using System;
using UnityEngine;

namespace ApocaDustStorm
{
    // Rebuilt while the existing 20 Hz particle pass is already reading positions.
    // Fixed storage and constant-time lookups avoid pairwise particle comparisons.
    internal sealed class DustOccupancy
    {
        private const int Width = 32, Height = 16;
        private const float Cell = 6;
        private readonly byte[] cells = new byte[Width * Width * Height];
        private readonly byte limit;
        private double x, y, z;
        internal DustOccupancy(byte maximum) { limit = Math.Max((byte)1, maximum); }
        internal void Begin(Vector3 eye)
        {
            Array.Clear(cells, 0, cells.Length);
            x = (Math.Floor(eye.x / Cell) - Width / 2) * Cell;
            y = (Math.Floor(eye.y / Cell) - 4) * Cell;
            z = (Math.Floor(eye.z / Cell) - Width / 2) * Cell;
        }
        private int Index(Vector3 point)
        {
            double dx = (point.x - x) / Cell, dy = (point.y - y) / Cell, dz = (point.z - z) / Cell;
            if (!(dx >= 0 && dx < Width && dy >= 0 && dy < Height && dz >= 0 && dz < Width)) return -1;
            return ((int)dy * Width + (int)dz) * Width + (int)dx;
        }
        internal void Observe(Vector3 point)
        {
            int cell = Index(point);
            if (cell >= 0 && cells[cell] < byte.MaxValue) cells[cell]++;
        }
        internal bool TryReserve(Vector3 point)
        {
            int cell = Index(point);
            // Outside the local grid, distance visibility still decides whether
            // emission is worthwhile. A grid edge must not become a visible wall.
            if (cell < 0) return true;
            if (cells[cell] >= limit) return false;
            cells[cell]++; return true;
        }
    }
}
