using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace MFR_Core;

public class MidiPlayer
{
    private int[] blackKeysInOctave = { 1, 3, 6, 8, 10 }; // C#, D#, F#, G#, A#
    private bool IsBlackKey(int midiNote)
    {
        return blackKeysInOctave.Contains((midiNote) % 12);
    }
    private SKColor GenerateTrackColor(int track, int totalTracks)
    {
        // Asegurarse de que el track esté en el rango correcto
        track = Math.Max(0, Math.Min(track, totalTracks - 1));

        // Dividir el espectro de colores (0-360 en HSL) entre los tracks
        float hue = (float)track / totalTracks * 360.0f;

        // Mantener la saturación y luminosidad constantes para un estilo consistente
        float saturation = 0.8f; // 80% de saturación
        float lightness = 0.6f; // 60% de luminosidad

        // Convertir HSL a RGB
        //return SKColor.FromHsl(hue, saturation, lightness);
        return HslToRgb(hue, saturation, lightness);
    }

    private SKColor HslToRgb(float hue, float saturation, float lightness)
    {
        float c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        float x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        float m = lightness - c / 2;

        float r = 0, g = 0, b = 0;

        if (hue >= 0 && hue < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (hue >= 60 && hue < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (hue >= 120 && hue < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (hue >= 180 && hue < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (hue >= 240 && hue < 300)
        {
            r = x; g = 0; b = c;
        }
        else if (hue >= 300 && hue < 360)
        {
            r = c; g = 0; b = x;
        }

        return new SKColor(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255)
        );
    }
    private static SKColor borderColor = SKColors.Black; // Color del borde
    private SKColor[] borderColorArray =
        [borderColor, borderColor, borderColor, borderColor];
	private MidiFile midiFile;
    private float width = 1920;
    private float height = 1080;
    private uint rendered = 0;
    private uint bpm = 0;
    private uint vertexOffset = 0;
    public double midiTime = 0;
    private double deltaMidi = 0;
    private double deltaTime = 0;
    private float screenBottom = 0;
    private float keyHeight = 0;
    private FastList<SKPoint> vertices = new();
    private FastList<SKColor> colorsv = new();
    private FastList<ushort> indices = new();
    private FastList<NoteRect> rects = new();
    private SKRect bounds = new SKRect();
    private static SKPaint _outline = new()
    {
        Color = SKColors.Black,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f
    };
    private SKPaint textPaint = new()
    {
        TextSize = 30,
        IsAntialias = true
    };
    private SKPaint paint = new SKPaint
    {
        Color = SKColors.White,
        IsAntialias = true,
    };
    
    private int MaxVerticesPerBatch = 300_000; // 4 vértices por nota (quad)
    public double scaleFactor = 1f;
    public float borderSize = 1.75f;
    private MidiNoteRenderer pk;
    private Key[] k;
    private SKColor[][] colorsvertexHelp;
    private (Dictionary<int, SKRect> KeyRects, float[] WhiteKeyCenters) pkrs;
    private string ksr;
    public double dur;
    private Process _ffmpeg;
    private bool ffmpeg;
    private Stream ffmpegInput;
    private IntPtr pixelsPtr;
    private byte[] pixels;
	public MidiPlayer(string path, int fps, int w, int h, bool ffmpeg, string outputPath)
    {
        width = w;
        height = h;
        this.ffmpeg = ffmpeg;
        if (ffmpeg)
        {
            var fs = File.CreateText($"MFR_ffmpeg_{DateTime.Now.ToString("s").Replace(":", "-")}.log");
            fs.AutoFlush = true;
            pixelsPtr = Marshal.AllocHGlobal(w * h * 4);
            pixels = new byte[w * h * 4];
            _ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -f rawvideo -pix_fmt bgra -s {w}x{h} -r 60 -i - -c:v h264_qsv -q:v 23 {outputPath}",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    RedirectStandardError = true, // ¡Importante para depurar
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            _ffmpeg.Start();
            _ffmpeg.OutputDataReceived += (sender, eventArgs) =>
            {
                fs.WriteLine($"ffmpeg stdout: {eventArgs.Data}");
                fs.Flush();
            };
            _ffmpeg.ErrorDataReceived += (sender, eventArgs) =>
            {
                fs.WriteLine($"ffmpeg stderr: {eventArgs.Data}");
                Console.WriteLine($"FFMPEG ERROR: {eventArgs.Data}");
                fs.Flush();
            };
            ffmpegInput = _ffmpeg.StandardInput.BaseStream;
        }
		midiFile = new(path);
        bpm = midiFile.BPM;
        deltaTime = 1d / fps;
        deltaMidi = (midiFile.PPQ * (bpm / 60.0) * deltaTime);
        k = new Key[255];
        for (int i = 0; i < k.Length; i++)
        {
            k[i] = new();
        }
        float keyHeight = (float)(height * 0.151);
        screenBottom = height - keyHeight;
        pk = new MidiNoteRenderer(yOffset: screenBottom, whiteKeyHeight: keyHeight, midiNoteMin: midiFile.MinKey, midiNoteMax: midiFile.MaxKey);
        pkrs = pk.GenerateKeys(width);
        colorsvertexHelp = new SKColor[midiFile.Tracks.Count()][];
        for (int i = 0; i < colorsvertexHelp.Length; i++)
        {
            var cc = GenerateTrackColor(i, colorsvertexHelp.Length);
            colorsvertexHelp[i] = [cc, cc, cc, cc];
        }
        dur = midiFile.GetDurationInMilliseconds();
        ksr = $"{TimeSpan.FromMilliseconds(dur):c}";
    }
    private void DrawCurrentBatch(SKCanvas image)
    {
        using (var skvertices = SKVertices.CreateCopy(
                   SKVertexMode.Triangles,
                   vertices.ToArray(),
                   null,
                   colorsv.ToArray(),
                   indices.ToArray()))
        {
            image.DrawVertices(skvertices, SKBlendMode.Modulate, paint);
        }
        vertices.Unlink();
        colorsv.Unlink();
        indices.Unlink();
        vertexOffset = 0;
    }

	public void Render(SKCanvas image, SKSurface surface)
    {
        rects.Unlink();
		image.Clear(SKColors.White);
        rendered = 0;
        foreach (var tempo in midiFile.TempoChanges)
        {
            if (tempo.Ticks >= midiTime && tempo.Ticks < midiTime + deltaMidi)
            {
                bpm = tempo.BPM;
                deltaMidi = (midiFile.PPQ * (tempo.BPM / 60.0) * deltaTime);
            }
        }
        midiFile.ParseUpTo(midiTime, midiTime + 600 + (deltaMidi * 20));
        foreach (var note in midiFile.Notes)
        {
            if (!(note.Key >= pk._midiNoteMin && note.Key <= pk._midiNoteMax))
            {
                continue;
            }
            if (!note.onPlayed && note.StartTime >= midiTime && note.StartTime <= (midiTime + deltaMidi))
            {
                note.SetonPlayed(true);
                k[note.Key].AddTrack(note.Track);
            }
            if (!note.Rendered && note.EndTime >= midiTime && note.EndTime <= (midiTime + deltaMidi))
            {
                note.SetRendered(true);
                k[note.Key].RemoveTrack(note.Track);
            }
            float noteHeight = (float)(note.Duration * scaleFactor);
            float noteY = (float)((midiTime - note.StartTime) * scaleFactor) + (height - noteHeight - keyHeight);
            if (noteY + noteHeight >= 0 && noteY <= screenBottom)
            {
                rendered++;
                float noteX = pkrs.KeyRects[note.Key].Left;
                float noteWidth = pkrs.KeyRects[note.Key].Width;
                // Calcular posición y tamaño de la nota
                var rect = SKRect.Create(noteX, noteY, noteWidth, noteHeight);
                #region border

                if (noteHeight > borderSize * 10)
                {
                    // Añadir vértices (4 por nota)
                    vertices.Add(new SKPoint(rect.Left - borderSize, rect.Top - borderSize));
                    vertices.Add(new SKPoint(rect.Right + borderSize, rect.Top - borderSize));
                    vertices.Add(new SKPoint(rect.Right + borderSize, rect.Bottom + borderSize));
                    vertices.Add(new SKPoint(rect.Left - borderSize, rect.Bottom + borderSize));
    
                    // Añadir colores (mismo color para los 4 vértices)
                    colorsv.AddRange(borderColorArray);
    
                    // Añadir índices (6 por nota: 2 triángulos)
                    indices.Add((ushort)(vertexOffset + 0));
                    indices.Add((ushort)(vertexOffset + 1));
                    indices.Add((ushort)(vertexOffset + 2));
                    indices.Add((ushort)(vertexOffset + 0));
                    indices.Add((ushort)(vertexOffset + 2));
                    indices.Add((ushort)(vertexOffset + 3));
    
                    vertexOffset += 4;
                }
                #endregion
                #region fill
                // Añadir vértices (4 por nota)
                vertices.Add(new SKPoint(rect.Left, rect.Top));
                vertices.Add(new SKPoint(rect.Right, rect.Top));
                vertices.Add(new SKPoint(rect.Right, rect.Bottom));
                vertices.Add(new SKPoint(rect.Left, rect.Bottom));
    
                // Añadir colores (mismo color para los 4 vértices)
                colorsv.AddRange(colorsvertexHelp[note.Track]);
    
                // Añadir índices (6 por nota: 2 triángulos)
                indices.Add((ushort)(vertexOffset + 0));
                indices.Add((ushort)(vertexOffset + 1));
                indices.Add((ushort)(vertexOffset + 2));
                indices.Add((ushort)(vertexOffset + 0));
                indices.Add((ushort)(vertexOffset + 2));
                indices.Add((ushort)(vertexOffset + 3));
                vertexOffset += 4;
                #endregion
    
                // Dibujar batch cuando alcance el límite
                if (vertices.Length >= MaxVerticesPerBatch)
                {
                    DrawCurrentBatch(image);
                }
            }
        }
        if (vertices.Length > 0)
        {
            DrawCurrentBatch(image);
        }

        foreach (var kvp in pkrs.KeyRects.Where(k => !IsBlackKey(k.Key)))
        {
            var track = k[kvp.Key].GetTopTrack();
            var c = SKColors.White;
            if (track != 0)
            {
                c = colorsvertexHelp[track - 1][0];
            }
            var blackKeyPaint = new SKPaint { Color = c };
            image.DrawRect(kvp.Value, blackKeyPaint);
            image.DrawRect(kvp.Value, _outline);
            blackKeyPaint.Dispose();
        }

        // Dibujar teclas negras
        foreach (var kvp in pkrs.KeyRects.Where(k => IsBlackKey(k.Key)))
        {
            var track = k[kvp.Key].GetTopTrack();
            var c = SKColors.Black;
            if (track != 0)
            {
                c = colorsvertexHelp[track - 1][0];
            }
            var blackKeyPaint = new SKPaint { Color = c };
            image.DrawRect(kvp.Value, blackKeyPaint);
            image.DrawRect(kvp.Value, _outline);
            blackKeyPaint.Dispose();
        }
        string text = $"MFR ({TimeSpan.FromMilliseconds(midiFile.ConvertTicksToSeconds((uint)midiTime)):c} / {ksr}) | Rendered Notes: {rendered:N0} ({midiFile.Notes.Count:N0}) | Ram Usage: {PublicObjects.SizeSuffix(Environment.WorkingSet)} | BPM: {bpm:N0} | PPQ: {midiFile.PPQ:N0}";
        textPaint.MeasureText(text, ref bounds);
        textPaint.Style = SKPaintStyle.Stroke;
        textPaint.Color = SKColors.Black;
        image.DrawText(text, 0, -bounds.Top, textPaint);
        textPaint.Style = SKPaintStyle.Fill;
        textPaint.Color = SKColors.White;
        image.DrawText(text, 0, -bounds.Top, textPaint);
        midiTime += deltaMidi;
        if (ffmpeg)
        {
            using (var snapshot = surface.Snapshot())
            using (var pixmap = snapshot.PeekPixels())
            {
                pixmap.ReadPixels(snapshot.Info, pixelsPtr, pixmap.RowBytes);
                Marshal.Copy(pixelsPtr, pixels, 0, pixels.Length);
                ffmpegInput.Write(pixels);
            }
        }
    }

    public void StopRender()
    {
        ffmpegInput?.Close();
        _ffmpeg?.WaitForExit();
    }
}