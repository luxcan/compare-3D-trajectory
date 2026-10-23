namespace Compare3DTrajectory.Core.Geometry;

/// <summary>
/// A static 3-d tree over a fixed set of points, for nearest-point and within-radius queries.
/// </summary>
/// <remarks>
/// The tree is implicit: each node is the median of its index range, so it costs one int array and
/// one axis byte per point and no node objects. Each range splits on its widest axis, which suits
/// trajectories - long and thin, and nearly flat.
/// </remarks>
internal sealed class KdTree
{
    private readonly Vec3[] _points;
    private readonly int[] _order;
    private readonly byte[] _axis;

    public KdTree(IReadOnlyList<Vec3> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            throw new ArgumentException("A tree needs at least one point.", nameof(points));
        }

        _points = [.. points];
        _order = new int[_points.Length];
        for (int i = 0; i < _order.Length; i++)
        {
            _order[i] = i;
        }

        _axis = new byte[_points.Length];
        Build(0, _order.Length);
    }

    /// <summary>The index of the point closest to <paramref name="query"/>.</summary>
    public int Nearest(Vec3 query, out double distanceSquared)
    {
        int best = -1;
        double bestDistanceSquared = double.PositiveInfinity;
        SearchNearest(0, _order.Length, query, ref best, ref bestDistanceSquared);
        distanceSquared = bestDistanceSquared;
        return best;
    }

    /// <summary>Adds to <paramref name="results"/> every point within <paramref name="radius"/> of the query.</summary>
    public void WithinRadius(Vec3 query, double radius, List<int> results)
    {
        SearchRadius(0, _order.Length, query, radius, radius * radius, results);
    }

    private void Build(int low, int high)
    {
        if (high - low <= 1)
        {
            return;
        }

        Vec3 min = _points[_order[low]];
        Vec3 max = min;
        for (int i = low + 1; i < high; i++)
        {
            Vec3 point = _points[_order[i]];
            min = Vec3.Min(min, point);
            max = Vec3.Max(max, point);
        }

        Vec3 extent = max - min;
        byte axis = extent.X >= extent.Y && extent.X >= extent.Z ? (byte)0 : extent.Y >= extent.Z ? (byte)1 : (byte)2;
        int middle = (low + high) / 2;
        SelectNth(low, high, middle, axis);
        _axis[middle] = axis;

        Build(low, middle);
        Build(middle + 1, high);
    }

    /// <summary>
    /// Reorders the range so the element at <paramref name="nth"/> is the one a sort would put there,
    /// with nothing larger before it and nothing smaller after it (Hoare's selection).
    /// </summary>
    private void SelectNth(int low, int high, int nth, int axis)
    {
        int left = low;
        int right = high - 1;
        while (right > left)
        {
            double pivot = _points[_order[(left + right) / 2]][axis];
            int i = left;
            int j = right;
            while (i <= j)
            {
                while (_points[_order[i]][axis] < pivot)
                {
                    i++;
                }

                while (_points[_order[j]][axis] > pivot)
                {
                    j--;
                }

                if (i <= j)
                {
                    (_order[i], _order[j]) = (_order[j], _order[i]);
                    i++;
                    j--;
                }
            }

            if (nth <= j)
            {
                right = j;
            }
            else if (nth >= i)
            {
                left = i;
            }
            else
            {
                return;
            }
        }
    }

    private void SearchNearest(int low, int high, Vec3 query, ref int best, ref double bestDistanceSquared)
    {
        if (low >= high)
        {
            return;
        }

        int middle = (low + high) / 2;
        Vec3 point = _points[_order[middle]];
        double distanceSquared = query.DistanceSquaredTo(point);
        if (distanceSquared < bestDistanceSquared)
        {
            bestDistanceSquared = distanceSquared;
            best = _order[middle];
        }

        int axis = _axis[middle];
        double offset = query[axis] - point[axis];
        if (offset < 0)
        {
            SearchNearest(low, middle, query, ref best, ref bestDistanceSquared);
            if (offset * offset < bestDistanceSquared)
            {
                SearchNearest(middle + 1, high, query, ref best, ref bestDistanceSquared);
            }
        }
        else
        {
            SearchNearest(middle + 1, high, query, ref best, ref bestDistanceSquared);
            if (offset * offset < bestDistanceSquared)
            {
                SearchNearest(low, middle, query, ref best, ref bestDistanceSquared);
            }
        }
    }

    private void SearchRadius(int low, int high, Vec3 query, double radius, double radiusSquared, List<int> results)
    {
        if (low >= high)
        {
            return;
        }

        int middle = (low + high) / 2;
        Vec3 point = _points[_order[middle]];
        if (query.DistanceSquaredTo(point) <= radiusSquared)
        {
            results.Add(_order[middle]);
        }

        int axis = _axis[middle];
        if (query[axis] - radius <= point[axis])
        {
            SearchRadius(low, middle, query, radius, radiusSquared, results);
        }

        if (query[axis] + radius >= point[axis])
        {
            SearchRadius(middle + 1, high, query, radius, radiusSquared, results);
        }
    }
}
