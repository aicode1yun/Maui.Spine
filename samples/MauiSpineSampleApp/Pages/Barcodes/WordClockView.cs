using Plugin.Maui.Spine.Barcodes;

namespace MauiSpineSampleApp.Pages.Barcodes;

/// <summary>
/// A 12 × 12 word clock drawn as it looks on the wall: every letter printed, the lit ones glowing. Its
/// <see cref="Matrix"/> decides which letters are lit, so a 12 × 12 Data Matrix turns the clock into a code.
/// </summary>
public sealed class WordClockView : GraphicsView, IDrawable
{
    private static readonly string[] Letters =
    [
        "ITLISASTENHA", "QUARTERTWENT", "FIVEXMINUTES", "PASTOTWELVEO",
        "ONETWOTHREEX", "FOURFIVESIXE", "SEVENEIGHTNI", "NINETENELEVE",
        "OCLOCKINTHEM", "MORNINGAFTER", "NOONEVENINGA", "ATNIGHTSPINE",
    ];

    public static readonly BindableProperty MatrixProperty = BindableProperty.Create(
        nameof(Matrix), typeof(BarcodeMatrix), typeof(WordClockView), propertyChanged: (b, _, _) => ((WordClockView)b).Invalidate());

    public WordClockView()
    {
        Drawable = this;
        HeightRequest = 320;
    }

    public BarcodeMatrix? Matrix
    {
        get => (BarcodeMatrix?)GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        float side = Math.Min(rect.Width, rect.Height);
        var face = new RectF(rect.Center.X - side / 2, rect.Center.Y - side / 2, side, side);
        canvas.FillColor = Color.FromRgb(12, 12, 14);
        canvas.FillRoundedRectangle(face, side * 0.03f);

        // A letter's width of black glass around the grid is the code's quiet zone
        float cell = side / 13.5f;
        float left = face.X + (side - 12 * cell) / 2, top = face.Y + (side - 12 * cell) / 2;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = cell * 0.55f;
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 12; x++)
            {
                bool lit = Matrix is { Width: 12, Height: 12 } m && m[x, y];
                var box = new RectF(left + x * cell, top + y * cell, cell, cell);
                string letter = Letters[y][x].ToString();
                if (lit)
                {
                    canvas.SetShadow(SizeF.Zero, cell * 0.35f, Color.FromRgba(255, 250, 230, 230));
                    canvas.FontColor = Colors.White;
                }
                else
                {
                    canvas.SetShadow(SizeF.Zero, 0, null);
                    canvas.FontColor = Color.FromRgb(90, 90, 92);
                }
                canvas.DrawString(letter, box, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        canvas.SetShadow(SizeF.Zero, 0, null);
    }
}
