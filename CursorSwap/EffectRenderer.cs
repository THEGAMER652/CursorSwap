using System.Drawing.Drawing2D;

namespace CursorSwap;

/// <summary>Draws one frame of a click animation. Shared by the real overlay and the in-app preview.</summary>
public static class EffectRenderer
{
    /// <summary>Largest distance (unscaled px) any effect reaches from its center.</summary>
    public const float Reach = 80f;
    public const float BaseDurationMs = 440f;

    public static ClickAnimation[] Selectable => Enum.GetValues<ClickAnimation>().Where(a => a != ClickAnimation.None).ToArray();

    static float E(float t) => Theme.EaseOut(t);
    static Color A(Color c, float a) => Theme.A(c, a);

    public static void Draw(Graphics g, ClickAnimation anim, float t, PointF c, Color col, float s, int seed)
    {
        t = Theme.Clamp01(t);
        float e = E(t), fade = 1f - t;
        var rnd = new Random(seed);
        var oldSmooth = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        switch (anim)
        {
            case ClickAnimation.Ripple:
                for (int i = 0; i < 3; i++)
                {
                    float lt = Theme.Clamp01(t * 1.45f - i * 0.22f);
                    if (lt <= 0 || lt >= 1) continue;
                    float r = (4 + E(lt) * 42) * s;
                    using var p = new Pen(A(col, 0.9f * (1 - lt)), Math.Max(0.6f, (3.4f - lt * 2.6f) * s));
                    g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
                }
                break;

            case ClickAnimation.Pulse:
            {
                float r = (6 + e * 28) * s;
                using (var b = new SolidBrush(A(col, 0.45f * fade))) g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2);
                float r2 = (5 - t * 3) * s;
                using (var b = new SolidBrush(A(col, fade))) g.FillEllipse(b, c.X - r2, c.Y - r2, r2 * 2, r2 * 2);
                break;
            }

            case ClickAnimation.Burst:
            {
                using var p = new Pen(A(col, fade), 2.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                for (int i = 0; i < 10; i++)
                {
                    double ang = i * Math.PI / 5 + rnd.NextDouble() * 0.15;
                    float r1 = (6 + e * 26) * s, r2 = (12 + e * 40) * s * (i % 2 == 0 ? 1f : 0.8f);
                    g.DrawLine(p, c.X + (float)Math.Cos(ang) * r1, c.Y + (float)Math.Sin(ang) * r1,
                                  c.X + (float)Math.Cos(ang) * r2, c.Y + (float)Math.Sin(ang) * r2);
                }
                break;
            }

            case ClickAnimation.Ring:
            {
                float r = (44 - e * 36) * s;
                using (var p = new Pen(A(col, 1f - t * t), 3f * s)) g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
                if (t > 0.6f)
                {
                    float k = (t - 0.6f) / 0.4f, r2 = (4 + k * 14) * s;
                    using var p2 = new Pen(A(col, 1 - k), 2f * s);
                    g.DrawEllipse(p2, c.X - r2, c.Y - r2, r2 * 2, r2 * 2);
                }
                break;
            }

            case ClickAnimation.Sparkle:
                for (int i = 0; i < 8; i++)
                {
                    double ang = i * Math.PI * 2 / 8 + rnd.NextDouble() * 0.7;
                    float dist = (6 + e * (28 + (float)rnd.NextDouble() * 18)) * s;
                    float size = (5 + (float)rnd.NextDouble() * 4) * s * (1 - t * 0.6f);
                    float rot = t * MathF.PI * (rnd.Next(2) == 0 ? 1 : -1);
                    var pos = new PointF(c.X + (float)Math.Cos(ang) * dist, c.Y + (float)Math.Sin(ang) * dist);
                    Color sc = i % 3 == 0 ? Color.White : Theme.HueShift(col, (float)rnd.NextDouble() * 40 - 20);
                    Star(g, pos, size, rot, A(sc, fade));
                }
                break;

            case ClickAnimation.Shockwave:
            {
                float r = (6 + e * 52) * s;
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(c.X - r, c.Y - r, r * 2, r * 2);
                    using var pb = new PathGradientBrush(gp)
                    {
                        CenterColor = A(col, 0f),
                        SurroundColors = new[] { A(col, 0.35f * fade) },
                        FocusScales = new PointF(0.75f, 0.75f)
                    };
                    g.FillPath(pb, gp);
                }
                using var p = new Pen(A(col, 0.9f * fade), Math.Max(0.8f, (9 * fade + 1) * s));
                g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
                break;
            }

            case ClickAnimation.Square:
            {
                for (int i = 0; i < 2; i++)
                {
                    float lt = Theme.Clamp01(t * 1.3f - i * 0.2f);
                    if (lt <= 0 || lt >= 1) continue;
                    float size = (10 + E(lt) * 40) * s;
                    var state = g.Save();
                    g.TranslateTransform(c.X, c.Y);
                    g.RotateTransform(45 + E(lt) * 90 * (i == 0 ? 1 : -1));
                    using var path = Theme.Round(new RectangleF(-size / 2, -size / 2, size, size), size * 0.22f);
                    using var p = new Pen(A(col, 1 - lt), 2.4f * s);
                    g.DrawPath(p, path);
                    g.Restore(state);
                }
                break;
            }

            case ClickAnimation.Confetti:
                for (int i = 0; i < 18; i++)
                {
                    double ang = rnd.NextDouble() * Math.PI * 2;
                    float speed = (26 + (float)rnd.NextDouble() * 34) * s;
                    float x = c.X + (float)Math.Cos(ang) * speed * e;
                    float y = c.Y + (float)Math.Sin(ang) * speed * e + 34 * s * t * t;
                    Color cc = Theme.HueShift(col, (float)rnd.NextDouble() * 160 - 80);
                    var state = g.Save();
                    g.TranslateTransform(x, y);
                    g.RotateTransform((float)rnd.NextDouble() * 360 + t * 540);
                    using var b = new SolidBrush(A(cc, 1 - t * t));
                    float w = 3.5f * s, h = 7f * s * (0.5f + 0.5f * MathF.Abs(MathF.Cos(t * 9 + i)));
                    g.FillRectangle(b, -w / 2, -h / 2, w, h);
                    g.Restore(state);
                }
                break;

            case ClickAnimation.Splash:
                for (int i = 0; i < 12; i++)
                {
                    double ang = -Math.PI / 2 + (rnd.NextDouble() - 0.5) * Math.PI * 1.6;
                    float dist = (10 + e * (22 + (float)rnd.NextDouble() * 22)) * s;
                    float x = c.X + (float)Math.Cos(ang) * dist;
                    float y = c.Y + (float)Math.Sin(ang) * dist + 46 * s * t * t;
                    float r = (1 + 3.6f * fade) * s;
                    using var b = new SolidBrush(A(col, fade));
                    g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
                }
                {
                    float r = (3 + e * 16) * s;
                    using var p = new Pen(A(col, 0.6f * fade), 2 * s);
                    g.DrawEllipse(p, c.X - r, c.Y - r * 0.45f, r * 2, r * 0.9f);
                }
                break;

            case ClickAnimation.Glow:
            {
                float r = (12 + e * 34) * s;
                using var gp = new GraphicsPath();
                gp.AddEllipse(c.X - r, c.Y - r, r * 2, r * 2);
                using var pb = new PathGradientBrush(gp)
                {
                    CenterColor = A(Color.White, 0.85f * fade),
                    SurroundColors = new[] { A(col, 0f) },
                };
                pb.SetSigmaBellShape(0.6f, 1f);
                pb.CenterColor = A(Theme.Lerp(col, Color.White, 0.4f), 0.85f * fade);
                g.FillPath(pb, gp);
                break;
            }
        }

        g.SmoothingMode = oldSmooth;
    }

    static void Star(Graphics g, PointF c, float r, float rot, Color col)
    {
        var pts = new PointF[8];
        for (int i = 0; i < 8; i++)
        {
            float rr = i % 2 == 0 ? r : r * 0.32f;
            float a = rot + i * MathF.PI / 4;
            pts[i] = new PointF(c.X + MathF.Cos(a) * rr, c.Y + MathF.Sin(a) * rr);
        }
        using var b = new SolidBrush(col);
        g.FillPolygon(b, pts);
    }
}
