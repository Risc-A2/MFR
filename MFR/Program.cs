using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace MFR
{
    public class Program
    {
        private static SKPaint _outline = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f
        };
        private static SKPaint _blackkey = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill
        };
        static SKColor GenerateTrackColor(int track, int totalTracks)
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

        static SKColor HslToRgb(float hue, float saturation, float lightness)
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

        private static int _nt = 127;
        static SKPoint[] GetBox(float x, float y, float width, float height)
        {
            return [
                /*
                            A--------------B /DUPLICATE
                            -              -
                            -              -
                            -              -
                 /DUPLICATE C--------------D
                 */
                new SKPoint(x, y),                 // Esquina superior izquierda: A
                new SKPoint(x + width, y),         // Esquina superior derecha: B
                new SKPoint(x, y + height),        // Esquina inferior izquierda : C
             
                new SKPoint(x + width, y),         // Esquina superior derecha (repetido): B
                new SKPoint(x + width, y + height), // Esquina inferior derecha: D
                new SKPoint(x, y + height)         // Esquina inferior izquierda (repetido): C
            ];
        }
        // Método para crear los uniforms de forma segura
        private static SKRuntimeEffectUniforms CreateNoteUniforms(SKColor color, float x, float y, float width, float height)
        {
            var uniforms = new SKRuntimeEffectUniforms(NoteShaderEffect);
    
            // u_color: vec4 (RGBA en formato float 0-1)
            uniforms.Add("u_color", new[] 
            {
                color.Red / 255f,
                color.Green / 255f,
                color.Blue / 255f,
                color.Alpha / 255f
            });
    
            // u_position: vec2 (X, Y)
            uniforms.Add("u_position", new[] { x, y });
    
            // u_size: vec2 (Width, Height)
            uniforms.Add("u_size", new[] { width, height });
    
            return uniforms;
        }
        const string NoteShaderCode = @"
        uniform vec4 u_color;
        uniform vec2 u_position;
        uniform vec2 u_size;

        vec4 main(vec2 fragCoord) {
            // Coordenadas normalizadas (0 a 1 dentro del rectángulo)
            vec2 uv = (fragCoord - u_position) / u_size;
            
            // Efecto de sombra (degradado vertical)
            float shadow = uv.y * 0.2;  // Ajusta el 0.5 para más/menos contraste
            
            // Color final con sombra
            return vec4(u_color.rgb * (1.0 - shadow), u_color.a);
        }";
        static string shaderError = "";
        
        static SKRuntimeEffect NoteShaderEffect = SKRuntimeEffect.CreateShader(NoteShaderCode, out shaderError);
        public static async Task Main(string[] args)
        {
            
            Console.WriteLine(shaderError);
            Console.Write("Press any key to continue but before check Shader Errors up...");
            Console.ReadKey();
            Console.WriteLine();
            double midiTime = 0;
            
            string midiFilePath = @"C:\Users\HUMAN\Videos\midi\mids\evans black DEATH 10 million.mid"; // Ruta al archivo MIDI
            string outputVideoPath = "output.mp4"; // Ruta de salida del video
            int fps = 60;
            Console.WriteLine("MFR | Midi Free Renderer");
            Console.WriteLine("Credits to:");
            Console.WriteLine("\tSkiaSharp team for making the SkiaSharp wrapper around Skia");
            Console.WriteLine("\tall people who worked/contributed to ffmpeg without this the program wouldn't render the midi as a video");
            Console.WriteLine("\tDeepSeek for giving ideas on how to optimize and Translating some C++ to C# (psst, its the MidiParser)");
            Console.WriteLine("\tRisc-A2 (Main creator of MFR)");
            // Leer el archivo MIDI
            Console.WriteLine("Loading midi...");
            MidiFile midiFile = new MidiFile(midiFilePath);
            double dur = midiFile.GetDurationInMilliseconds();
            //Console.WriteLine($"\tMidi loaded\n\tNote count: {midiFile.Notes.Count:N0} ({(midiFile.Notes.Count * 21):N0} bytes?)\n\tTempo changes count: {midiFile.TempoChanges.Length:N0} ({(midiFile.TempoChanges.Length * 12):N0} Bytes?)\n\tEvent count: {evtCount:N0} ({(evtCount * 8):N0} Bytes?)\n\tDuration (ms): {dur:N2} ({TimeSpan.FromMilliseconds(dur).ToString("c")})");
            Console.Write("Press any key to render...");
            Console.ReadKey();
            Console.WriteLine();
            var fs = File.CreateText($"MFR_{DateTime.Now.ToString("s").Replace(":", "-")}.log");
            fs.AutoFlush = true;
            Console.WriteLine("Creating SKPaint and Delta");
            double deltaTime = 1d / fps;
            double deltaMidi = (midiFile.PPQ * (midiFile.BPM / 60.0) * deltaTime);
            var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
            };

            int width = 1920;
            int height = 1080;
            var info = new SKImageInfo(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque);
            SKBitmap surface = new SKBitmap(info);
            var image = new SKCanvas(surface);
            Console.WriteLine("Starting ffmpeg");
            // Inicializar FFmpeg
            Process ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    //Arguments = $"-y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - -c:v libx264 -pix_fmt yuv420p {outputVideoPath}",
                    //h264_qsv
                    // usar h264_qsv para acceleracion por hardware
                    Arguments = $"-y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - -c:v h264_qsv -q:v 23 -pix_fmt nv12 {outputVideoPath}",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true, // ¡Importante para depurar
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            ffmpeg.OutputDataReceived += (sender, eventArgs) =>
            {
                fs.WriteLine($"ffmpeg stdout: {eventArgs.Data}");
            };
            ffmpeg.ErrorDataReceived += (sender, eventArgs) =>
            {
                fs.WriteLine($"ffmpeg stderr: {eventArgs.Data}");
                Console.WriteLine($"FFMPEG ERROR: {eventArgs.Data}");
            };

            ffmpeg.Start();
            Stream ffmpegInput = ffmpeg.StandardInput.BaseStream;
            Console.WriteLine("Preparing colors");
            float keyHeight = (float)(height * 0.151);
            float keyWidth = (float)(width / _nt);
            float screenBottom = height - keyHeight;
            var pk = new MidiNoteRenderer(yOffset: screenBottom, whiteKeyHeight: keyHeight, midiNoteMin: midiFile.MinKey, midiNoteMax: midiFile.MaxKey);
            var pkrs = pk.GenerateKeys(width);
            _nt = pkrs.KeyRects.Count;
            bool[] WhitePianoKeys = new bool[_nt];
            bool[] keyLayout = {true, false, true, false, true, true, false, true, false, true, false, true};
            SKColor[] colors = new SKColor[midiFile.Tracks.Count()];
            SKColor[][] colorsvertexHelp = new SKColor[midiFile.Tracks.Count()][];
            SKPaint[] pianoKeys = new SKPaint[_nt];
            SKPaint[] blackpianoKeys = new SKPaint[_nt];
            int lyt = 0;
            for (int i = 0; i < pianoKeys.Length; i++)
            {
                if (lyt >= 12)
                {
                    lyt = 0;
                }   
                WhitePianoKeys[i] = keyLayout[lyt];
                ++lyt;
                pianoKeys[i] = new();
                pianoKeys[i].Color = SKColors.White;
                if (!WhitePianoKeys[i])
                {
                    blackpianoKeys[i] = new();
                    blackpianoKeys[i].Color = SKColors.Black;
                }
            }

            for (int i = 0; i < colors.Length; i++)
            {
                var cc = GenerateTrackColor(i, colors.Length);
                colors[i] = cc;
                colorsvertexHelp[i] = [cc, cc, cc, cc];
            }

            Console.WriteLine("Preparing Piano SKRect's");
            Key[] k = new Key[255];
            for (int i = 0; i < k.Length; i++)
            {
                k[i] = new();
            }
            Console.WriteLine("Creating vertices, colors arrays");
            FastList<SKPoint> vertices = new();
            FastList<SKColor> colorsv = new();
            FastList<ushort> indices = new();
            var borderIndices = new FastList<ushort>();
            var borderVertices = new FastList<SKPoint>();
            FastList<SKColor> bordercolorsv = new();
            
            var borderColor = SKColors.Black; // Color del borde
            SKColor[] borderColorArray =
                [borderColor, borderColor, borderColor, borderColor];
            Console.WriteLine("Allocating managed raw image");
            byte[] buffer = new byte[info.Width * info.Height * 4]; // 3 bytes por píxel (RGB), 4 bytes 
            Console.WriteLine("Allocating unmanaged raw image pointer");
            var ptr = Marshal.AllocHGlobal(buffer.Length);
            GC.AddMemoryPressure(buffer.Length);
            float thickness = 2;
            Console.WriteLine("Rendering...");
            var pb = new ParallelProgressBar(dur);
            float scaleFactor = 1f;
            double dtCalc = deltaMidi;
            float ss = screenBottom - keyHeight;
            int lastIndex = 0;
            int idx = lastIndex;
            int rendered = 0;
            FastList<NoteRect> rects = new();
            SKRect bounds = new SKRect();
            SKPaint textPaint = new();
            textPaint.TextSize = 30;
            textPaint.IsAntialias = true;
            var m = new CpuMonitor();
            var gcc = GC.GetGCMemoryInfo();
            var installed = gcc.TotalAvailableMemoryBytes;
            float borderSize = 1.75f;
            uint bpm = midiFile.BPM;
            var ksr = $"{TimeSpan.FromMilliseconds(dur):c}";
            int vertexOffset = 0;
            int MaxVerticesPerBatch = 65536 / 2; // 4 vértices por nota (quad)
            void DrawCurrentBatch()
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
            while (!midiFile.Tracks.All(track => track.endOfTrack))
            //while (midiTime < 10000)
            {
                rects.Unlink();/*
                vertices.Unlink();
                colorsv.Unlink();
                indices.Unlink();*/
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
                        float noteWidth = pkrs.KeyRects[note.Key].Width;/*
                        vertices.Add(new SKPoint(noteX, noteY));
                        vertices.Add(new SKPoint(noteX + noteWidth, noteY));
                        vertices.Add(new SKPoint(noteX + noteWidth, noteY + noteHeight));
                        vertices.Add(new SKPoint(noteX, noteY + noteHeight));
                        
                        ushort baseIndex = (ushort)(vertices.Length);
                        colorsv.AddRange(colorsvertexHelp[note.Track]);

                        // Añade índices (2 triángulos por cuadrado)
                        indices.Add(baseIndex);
                        indices.Add((ushort)(baseIndex + 1));
                        indices.Add((ushort)(baseIndex + 2));
                        indices.Add(baseIndex);
                        indices.Add((ushort)(baseIndex + 2));
                        indices.Add((ushort)(baseIndex + 3));
                        */
                        
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
                            DrawCurrentBatch();
                        }
                        
                        /*
                        rects.Add(new()
                        {
                            X = noteX,
                            Y = noteY,
                            Width = pkrs.KeyRects[note.Key].Width,
                            Height = noteHeight,
                            Color = colors[note.Track]
                        });
                        */
                    }
                }
                if (vertices.Length > 0)
                {
                    DrawCurrentBatch();
                }

                idx = lastIndex;/*
                for (; idx < midiFile.Notes.Count; idx++)
                {
                    var note = midiFile.Notes[idx];
                    if (!note.Rendered && note.EndTime < midiTime)
                    {
                        note.SetRendered(true);
                    }
                    if (!note.onPlayed && note.StartTime >= midiTime && note.StartTime <= (midiTime + deltaMidi))
                    {
                        note.SetonPlayed(true);
                        k[note.Key].AddTrack(note.Track);
                    }
                    if (note.EndTime >= midiTime && note.EndTime <= (midiTime + deltaMidi))
                    {
                        k[note.Key].RemoveTrack(note.Track);
                    }
                    float noteHeight = (float)(note.Duration * scaleFactor);
                    float noteY = (float)((midiTime - note.StartTime) * scaleFactor) + (height - noteHeight - keyHeight);
                    if (midiFile.Notes[lastIndex].Rendered && note.EndTime > midiFile.Notes[lastIndex].EndTime)
                    {
                        lastIndex = idx;
                        while (midiFile.Notes[lastIndex].Rendered && note.EndTime > midiFile.Notes[lastIndex].EndTime) {
                            ++lastIndex;
                        }
                    } 
                    //else if (note.StartTime <= midiTime + deltaMidi && note.EndTime >= midiTime)
                    else if (noteY + noteHeight >= 0 && noteY <= screenBottom)
                    {
                        rendered++;
                        float noteX = pkrs.KeyRects[note.Key].Left;
                        rects.Add(new()
                        {
                            x = noteX,
                            y = noteY,
                            width = pkrs.KeyRects[note.Key].Width,
                            height = noteHeight,
                            Color = colors[note.Track - 1]
                        });
                        
                    }
                    else if (noteY + noteHeight <= 0 && noteY >= screenBottom)
                    {
                        break;
                    }
                }*/
                /*
                foreach (var note in rects) {
                    // Añade vértices de la nota (4 por cuadrado)
                    vertices.Add(new SKPoint(note.X, note.Y));
                    vertices.Add(new SKPoint(note.X + note.Width, note.Y));
                    vertices.Add(new SKPoint(note.X + note.Width, note.Y + note.Height));
                    vertices.Add(new SKPoint(note.X, note.Y + note.Height));
                    float shadow = (note.Y / height) * 0.2f; // Ejemplo de sombra
                    var shadedColor = new SKColor(
                        (byte)(note.Color.Red * (1 - shadow)),
                        (byte)(note.Color.Green * (1 - shadow)),
                        (byte)(note.Color.Blue * (1 - shadow))
                    );
                    ushort baseIndex = (ushort)(vertices.Length);
                    colorsv.Add(shadedColor);
                    colorsv.Add(shadedColor);
                    colorsv.Add(shadedColor);
                    colorsv.Add(shadedColor);

                    // Añade índices (2 triángulos por cuadrado)
                    indices.Add(baseIndex);
                    indices.Add((ushort)(baseIndex + 1));
                    indices.Add((ushort)(baseIndex + 2));
                    indices.Add(baseIndex);
                    indices.Add((ushort)(baseIndex + 2));
                    indices.Add((ushort)(baseIndex + 3));
                }*/
                /*
                var verticesBatch = SKVertices.CreateCopy(
                    SKVertexMode.Triangles,
                    vertices.ToArray(),
                    null, // UVs (opcional)
                    colorsv.ToArray(), // Colores por vértice
                    indices.ToArray()
                );

                image.DrawVertices(verticesBatch, SKBlendMode.Modulate, paint);

                verticesBatch.Dispose();*/

/*
                await Parallel.ForEachAsync(rects, async (rect, token) =>
                {
                    lock (paint)
                    {
                        paint.Color = rect.Color;
                        var r = SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height);
                        image.DrawRect(r, paint);
                        image.DrawRect(r, _outline);
                    }
                });*//*
                foreach (var rect in rects)
                {
                    paint.Color = rect.Color;/*
                    var uniforms = CreateNoteUniforms(
                        rect.Color,
                        rect.X,
                        rect.Y,
                        rect.Width,
                        rect.Height
                    );
                    // Dibuja la nota (¡con sombra incluida!)
                    pt.Shader = NoteShaderEffect.ToShader(uniforms);*
                    var r = SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height);
                    image.DrawRect(r, paint);
                    image.DrawRect(r, _outline);
                    //pt.Shader.Dispose();
                    //uniforms.Dispose();
                }*/
                foreach (var kvp in pkrs.KeyRects.Where(k => !IsBlackKey(k.Key)))
                {
                    var track = k[kvp.Key].GetTopTrack();
                    var c = SKColors.White;
                    if (track != 0)
                    {
                        c = colors[track - 1];
                    }/*
                    var blackKeyUniforms = CreateNoteUniforms(
                        c,
                        kvp.Value.Left,
                        kvp.Value.Top,
                        kvp.Value.Width,
                        kvp.Value.Height
                    );*/
                    //var blackKeyPaint = new SKPaint { Shader = NoteShaderEffect.ToShader(blackKeyUniforms) };
                    var blackKeyPaint = new SKPaint { Color = c };
                    image.DrawRect(kvp.Value, blackKeyPaint);
                    image.DrawRect(kvp.Value, _outline);
                    //blackKeyPaint.Shader.Dispose();
                    blackKeyPaint.Dispose();
                    //blackKeyUniforms.Dispose();
                }

                // Dibujar teclas negras
                foreach (var kvp in pkrs.KeyRects.Where(k => IsBlackKey(k.Key)))
                {
                    var track = k[kvp.Key].GetTopTrack();
                    var c = SKColors.Black;
                    if (track != 0)
                    {
                        c = colors[track - 1];
                    }/*
                    var blackKeyUniforms = CreateNoteUniforms(
                        c,
                        kvp.Value.Left,
                        kvp.Value.Top,
                        kvp.Value.Width,
                        kvp.Value.Height
                    );*/
                    //var blackKeyPaint = new SKPaint { Shader = NoteShaderEffect.ToShader(blackKeyUniforms) };
                    var blackKeyPaint = new SKPaint { Color = c };
                    image.DrawRect(kvp.Value, blackKeyPaint);
                    image.DrawRect(kvp.Value, _outline);
                    //blackKeyPaint.Shader.Dispose();
                    blackKeyPaint.Dispose();
                    //blackKeyUniforms.Dispose();
                }
                string text = $"MFR ({TimeSpan.FromMilliseconds(midiFile.ConvertTicksToSeconds((uint)midiTime)):c} / {ksr}) | Rendered Notes: {rendered:N0} ({midiFile.Notes.Count:N0}) | Ram Usage: {PublicObjects.SizeSuffix(Environment.WorkingSet)} | CPU Usage: {m.GetCurrentCpuUsage()*100:#,000} | BPM: {bpm:N0} | PPQ: {midiFile.PPQ:N0}";
                textPaint.MeasureText(text, ref bounds);
                textPaint.Style = SKPaintStyle.Stroke;
                textPaint.Color = SKColors.Black;
                image.DrawText(text, 0, -bounds.Top, textPaint);
                textPaint.Style = SKPaintStyle.Fill;
                textPaint.Color = SKColors.White;
                image.DrawText(text, 0, -bounds.Top, textPaint);
                
                surface.PeekPixels().ReadPixels(info, ptr, info.RowBytes, 0, 0);

                Marshal.Copy(ptr, buffer, 0, buffer.Length);
                //pb.Update(deltaMidi);
                Console.Write($"\r{text}");
                midiTime += deltaMidi;
                ffmpegInput.Write(buffer, 0, buffer.Length);
            }

            Console.WriteLine("Freeing pointer");
            Marshal.FreeHGlobal(ptr);
            GC.RemoveMemoryPressure(buffer.Length);
            Console.WriteLine("Freeing image & canvas & Vertex & Colors");
            image.Dispose();
            surface.Dispose();
            vertices.Dispose();
            colorsv.Dispose();
            Console.WriteLine("Waiting ffmpeg to output the video...");
            // Cerrar FFmpeg y guardar el video
            ffmpegInput.Close();
            ffmpeg.WaitForExit();
            fs.Close();
            Console.WriteLine($"video saved in {outputVideoPath}");
            Console.Write("Press any key to exit...");
            Console.ReadKey();
            return;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        static SKRect[] _wp;
        static SKRect[] _bp;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private static bool IsBlackKey(int midiNote)
        {
            int[] blackKeysInOctave = { 1, 3, 6, 8, 10 }; // C#, D#, F#, G#, A#
            return blackKeysInOctave.Contains((midiNote) % 12);
        }
    }
}
