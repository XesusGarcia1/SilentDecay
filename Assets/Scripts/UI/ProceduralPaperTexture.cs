using UnityEngine;

/// <summary>
/// Generador procedural de texturas de papel envejecido y arrugado para la interfaz gráﬁca (OnGUI).
/// </summary>
public static class ProceduralPaperTexture
{
    private static Texture2D _cachedTex;

    /// <summary>
    /// Devuelve la textura de papel envejecido y arrugado. Si no existe, la genera y la cachea.
    /// </summary>
    public static Texture2D GetPaperTexture()
    {
        if (_cachedTex == null)
        {
            _cachedTex = GenerateOldPaperTexture(512, 512);
        }
        return _cachedTex;
    }

    private static Texture2D GenerateOldPaperTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h);
        Color[] pixels = new Color[w * h];

        // Colores base para envejecido (beige/pergamino cálido)
        Color baseColor = new Color(0.91f, 0.86f, 0.70f); // Amarillo pergamino
        Color darkBorderColor = new Color(0.62f, 0.51f, 0.33f); // Borde quemado/sucio
        Color stainColor = new Color(0.79f, 0.71f, 0.52f); // Manchas de humedad/suciedad

        // 1. Llenar con ruido base de textura de fibra de papel y manchas
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Coordenadas normalizadas [0, 1]
                float nx = (float)x / w;
                float ny = (float)y / h;

                // Ruido Perlin para manchas de humedad grandes
                float noiseLarge = Mathf.PerlinNoise(nx * 3.5f + 1.2f, ny * 3.5f + 2.5f);
                // Ruido Perlin medio para suciedad
                float noiseMed = Mathf.PerlinNoise(nx * 14f + 5.7f, ny * 14f + 8.1f);
                // Ruido fino para textura de grano de papel
                float noiseFine = Mathf.PerlinNoise(nx * 180f, ny * 180f);

                // Color de fondo con manchas de humedad
                Color col = Color.Lerp(baseColor, stainColor, noiseLarge * 0.35f);
                
                // Mezclar suciedad media
                col = Color.Lerp(col, darkBorderColor, noiseMed * 0.12f);
                
                // Grano fino (textura táctil)
                col += new Color(noiseFine, noiseFine, noiseFine) * 0.05f - new Color(0.025f, 0.025f, 0.025f);

                // 2. Viñeteado / Bordes oscurecidos (Efecto pergamino quemado/gastado)
                float distToEdgeX = Mathf.Min(nx, 1f - nx);
                float distToEdgeY = Mathf.Min(ny, 1f - ny);
                float distToEdge = Mathf.Min(distToEdgeX, distToEdgeY);

                if (distToEdge < 0.12f)
                {
                    float factor = 1f - (distToEdge / 0.12f);
                    // Añadir un poco de irregularidad al borde con Perlin
                    float edgeNoise = Mathf.PerlinNoise(nx * 25f, ny * 25f) * 0.25f;
                    factor = Mathf.Clamp01(factor + edgeNoise);
                    col = Color.Lerp(col, darkBorderColor, factor * 0.75f);
                }

                col.a = 1f; // Forzar opacidad total (evita transparencia accidental por aritmética de resta de Color)
                pixels[y * w + x] = col;
            }
        }
        tex.SetPixels(pixels);

        // 3. Dibujar arrugas/pliegues procedurales (creases)
        // Usamos una semilla fija de Random para que la arruga sea consistente cada vez que se genera
        Random.State prevState = Random.state;
        Random.InitState(1337); // Semilla fija para consistencia visual

        int numWrinkles = 10;
        for (int i = 0; i < numWrinkles; i++)
        {
            Vector2 p1 = new Vector2(Random.Range(0, w), Random.Range(0, h));
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float length = Random.Range(70f, 220f);
            Vector2 p2 = p1 + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;

            DrawWrinkleLine(tex, p1, p2, w, h);
        }

        Random.state = prevState; // Restaurar estado de Random

        tex.Apply();
        return tex;
    }

    private static void DrawWrinkleLine(Texture2D tex, Vector2 p1, Vector2 p2, int w, int h)
    {
        float steps = Mathf.Max(Mathf.Abs(p2.x - p1.x), Mathf.Abs(p2.y - p1.y));
        if (steps == 0) return;

        float dx = (p2.x - p1.x) / steps;
        float dy = (p2.y - p1.y) / steps;

        float cx = p1.x;
        float cy = p1.y;

        float px = -dy;
        float py = dx;
        float len = Mathf.Sqrt(px * px + py * py);
        if (len > 0f)
        {
            px /= len;
            py /= len;
        }

        for (int s = 0; s <= steps; s++)
        {
            int ix = Mathf.RoundToInt(cx);
            int iy = Mathf.RoundToInt(cy);

            // Línea de Sombra (oscura)
            if (ix >= 0 && ix < w && iy >= 0 && iy < h)
            {
                Color orig = tex.GetPixel(ix, iy);
                tex.SetPixel(ix, iy, Color.Lerp(orig, new Color(0.32f, 0.25f, 0.15f), 0.25f));
            }

            // Línea de Luz (clara) paralela (offset de 1.5 píxeles)
            int lx = Mathf.RoundToInt(cx + px * 1.5f);
            int ly = Mathf.RoundToInt(cy + py * 1.5f);
            if (lx >= 0 && lx < w && ly >= 0 && ly < h)
            {
                Color orig = tex.GetPixel(lx, ly);
                tex.SetPixel(lx, ly, Color.Lerp(orig, Color.white, 0.22f));
            }

            cx += dx;
            cy += dy;
        }
    }
}
