using System;

namespace Deadhaul.Core
{
    /// <summary>Deterministische hashes: dezelfde seed geeft overal exact dezelfde wereld.</summary>
    public static class Hash
    {
        public static float H2(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 0x27d4eb2d) ^ (uint)(z * 0x165667b1) ^ (uint)(seed * (int)0x9e3779b1);
                h = (h ^ (h >> 15)) * 0x85ebca6b;
                h = (h ^ (h >> 13)) * 0xc2b2ae35;
                h ^= h >> 16;
                return h / 4294967296f;
            }
        }

        public static float H3(int x, int y, int z, int seed)
        {
            unchecked { return H2((x * 0x3c6ef372) ^ y, z, seed); }
        }
    }

    /// <summary>Simplex-ruis (2D en 3D) met eigen permutatietabel per seed.</summary>
    public sealed class Simplex
    {
        static readonly float F2 = 0.5f * (MathF.Sqrt(3f) - 1f), G2 = (3f - MathF.Sqrt(3f)) / 6f;
        const float F3 = 1f / 3f, G3 = 1f / 6f;
        static readonly float[] Grad3 = {
            1,1,0, -1,1,0, 1,-1,0, -1,-1,0, 1,0,1, -1,0,1, 1,0,-1, -1,0,-1, 0,1,1, 0,-1,1, 0,1,-1, 0,-1,-1 };

        readonly byte[] perm = new byte[512];
        readonly byte[] permMod12 = new byte[512];

        public Simplex(int seed)
        {
            var p = new byte[256];
            for (int i = 0; i < 256; i++) p[i] = (byte)i;
            var rnd = new Random(seed);
            for (int i = 255; i > 0; i--) { int j = rnd.Next(i + 1); (p[i], p[j]) = (p[j], p[i]); }
            for (int i = 0; i < 512; i++) { perm[i] = p[i & 255]; permMod12[i] = (byte)(perm[i] % 12); }
        }

        static int FastFloor(float x) { int i = (int)x; return x < i ? i - 1 : i; }

        public float Noise2(float xin, float yin)
        {
            float n0 = 0, n1 = 0, n2 = 0;
            float s = (xin + yin) * F2;
            int i = FastFloor(xin + s), j = FastFloor(yin + s);
            float t = (i + j) * G2;
            float x0 = xin - (i - t), y0 = yin - (j - t);
            int i1 = x0 > y0 ? 1 : 0, j1 = x0 > y0 ? 0 : 1;
            float x1 = x0 - i1 + G2, y1 = y0 - j1 + G2;
            float x2 = x0 - 1 + 2 * G2, y2 = y0 - 1 + 2 * G2;
            int ii = i & 255, jj = j & 255;
            float t0 = 0.5f - x0 * x0 - y0 * y0;
            if (t0 > 0) { int g = permMod12[ii + perm[jj]] * 3; t0 *= t0; n0 = t0 * t0 * (Grad3[g] * x0 + Grad3[g + 1] * y0); }
            float t1 = 0.5f - x1 * x1 - y1 * y1;
            if (t1 > 0) { int g = permMod12[ii + i1 + perm[jj + j1]] * 3; t1 *= t1; n1 = t1 * t1 * (Grad3[g] * x1 + Grad3[g + 1] * y1); }
            float t2 = 0.5f - x2 * x2 - y2 * y2;
            if (t2 > 0) { int g = permMod12[ii + 1 + perm[jj + 1]] * 3; t2 *= t2; n2 = t2 * t2 * (Grad3[g] * x2 + Grad3[g + 1] * y2); }
            return 70f * (n0 + n1 + n2);
        }

        public float Noise3(float xin, float yin, float zin)
        {
            float n0, n1, n2, n3;
            float s = (xin + yin + zin) * F3;
            int i = FastFloor(xin + s), j = FastFloor(yin + s), k = FastFloor(zin + s);
            float t = (i + j + k) * G3;
            float x0 = xin - (i - t), y0 = yin - (j - t), z0 = zin - (k - t);
            int i1, j1, k1, i2, j2, k2;
            if (x0 >= y0)
            {
                if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
                else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
                else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
            }
            else
            {
                if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
                else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
                else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
            }
            float x1 = x0 - i1 + G3, y1 = y0 - j1 + G3, z1 = z0 - k1 + G3;
            float x2 = x0 - i2 + 2 * G3, y2 = y0 - j2 + 2 * G3, z2 = z0 - k2 + 2 * G3;
            float x3 = x0 - 1 + 3 * G3, y3 = y0 - 1 + 3 * G3, z3 = z0 - 1 + 3 * G3;
            int ii = i & 255, jj = j & 255, kk = k & 255;
            float t0 = 0.6f - x0 * x0 - y0 * y0 - z0 * z0;
            if (t0 < 0) n0 = 0; else { int g = permMod12[ii + perm[jj + perm[kk]]] * 3; t0 *= t0; n0 = t0 * t0 * (Grad3[g] * x0 + Grad3[g + 1] * y0 + Grad3[g + 2] * z0); }
            float t1 = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
            if (t1 < 0) n1 = 0; else { int g = permMod12[ii + i1 + perm[jj + j1 + perm[kk + k1]]] * 3; t1 *= t1; n1 = t1 * t1 * (Grad3[g] * x1 + Grad3[g + 1] * y1 + Grad3[g + 2] * z1); }
            float t2 = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
            if (t2 < 0) n2 = 0; else { int g = permMod12[ii + i2 + perm[jj + j2 + perm[kk + k2]]] * 3; t2 *= t2; n2 = t2 * t2 * (Grad3[g] * x2 + Grad3[g + 1] * y2 + Grad3[g + 2] * z2); }
            float t3 = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
            if (t3 < 0) n3 = 0; else { int g = permMod12[ii + 1 + perm[jj + 1 + perm[kk + 1]]] * 3; t3 *= t3; n3 = t3 * t3 * (Grad3[g] * x3 + Grad3[g + 1] * y3 + Grad3[g + 2] * z3); }
            return 32f * (n0 + n1 + n2 + n3);
        }

        public float Fbm2(float x, float z, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float a = 1, f = 1, sum = 0, norm = 0;
            for (int i = 0; i < octaves; i++) { sum += a * Noise2(x * f, z * f); norm += a; a *= gain; f *= lacunarity; }
            return sum / norm;
        }
    }
}
