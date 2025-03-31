using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace MFR_Core;

public readonly struct MidiEvent
{
    public MidiEvent(EventType _event, byte key, byte velocity, uint delta, byte channel)
    {
        Event = _event;
        Key = key;
        Velocity = velocity;
        DeltaTick = delta;
        Channel = channel;
    }
    public enum EventType : byte
    {
        NoteOff,
        NoteOn,
        Other
    }

    public readonly EventType Event;
    public readonly byte Key;
    public readonly byte Velocity;
    public readonly uint DeltaTick;
    public readonly byte Channel = 0;

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is MidiEvent evt && evt.Key == Key && evt.Event == Event && evt.Velocity == Velocity &&
               evt.DeltaTick == DeltaTick;
    }
}

public struct MidiTempoChange
{
    public uint Ticks;
    public uint Tempo;
    public uint BPM;
}
public struct MidiNote
{
    public byte Key = 0;
    public byte Velocity = 0;
    public uint StartTime = 0;
    public uint EndTime = 0;
    public float Duration = 0;
    public uint Track = 0;
    public byte Channel = 0;
    public bool onPlayed = false;
    public bool Rendered = false;

    public MidiNote()
    {
        Key = 0;
        Velocity = 0;
        StartTime = 0;
        EndTime = 0;
        Duration = 0;
        Track = 0;
        Channel = 0;
    }

    public void SetonPlayed(bool val)
    {
        onPlayed = val;
    }
    public void SetRendered(bool val)
    {
        Rendered = val;
    }


    public void SetDuration(float val)
    {
        Duration = val;
    }
}

public class MidiTrack(string fileName, long position, MidiFile files, uint track)
{
    private BinaryReader _reader;
    public BinaryReader reader
    {
        get
        {
            return _reader;
        }
    }
    public string Name;
    public string Instrument;
    //public FastList<MidiNote> Notes = new FastList<MidiNote>();
    public FastList<MidiTempoChange> TempoChanges = new();
    public FastList<MidiNote> ActiveNotes = new();
    public byte MaxNote = 64;
    public long stoppoint = 0;
    public byte MinNote = 64;
    uint Swap32(uint value)
    {
        return ((value >> 24) & 0xff) | ((value << 8) & 0xff0000) | ((value >> 8) & 0xff00) | ((value << 24) & 0xff000000);
    }
    uint ReadValue()
    {
        uint value = 0;
        byte byteRead = reader.ReadByte();

        value = byteRead;

        if ((value & 0x80) != 0)
        {
            value &= 0x7F;
            do
            {
                byteRead = reader.ReadByte();
                value = (uint)((value << 7) | (byteRead & 0x7F));
            } while ((byteRead & 0x80) != 0);
        }

        return value;
    }

    string ReadString(uint length)
    {
        return new string(reader.ReadChars((int)length));
    }

    private byte previousStatus = 0;
    private uint wallTime = 0;
    public bool endOfTrack = false;
    public uint maxwallTime = 0;

    public void ParseUpTo(double end)
    {
        if (endOfTrack)
        {
            return;
        }

        bool shouldstilldo = false;
        while (wallTime < end || shouldstilldo)
        {
            if (endOfTrack)
            {
                return;
            }
            var evt = ReadNextEvent();
            if (evt.HasValue)
            {
                if (ProcessNote(evt.Value))
                {
                    files.Events.Add(evt.Value);
                }
            }

            shouldstilldo = ActiveNotes.Length != 0;
        }
    }
    private bool ProcessNote(MidiEvent evt)
    {
        if (evt.Event == MidiEvent.EventType.NoteOn)
        {
            ActiveNotes.Add(new MidiNote
            {
                Key = evt.Key,
                Velocity = evt.Velocity,
                StartTime = wallTime,
                Channel = evt.Channel,
                Track = track,
                Duration = 69420f // Se calculará al recibir el NoteOff
            });
            return false;
        }
        else if (evt.Event == MidiEvent.EventType.NoteOff)
        {
            MidiNote? note = null;
            // Busca la nota activa correspondiente y actualiza su EndTime/Duration
            foreach (var n in ActiveNotes)
            {
                if (n.Key == evt.Key && n.Channel == evt.Channel)
                {
                    note = n;
                    break;
                }
            }

            if (note.HasValue)
            {
                var notef = note.Value;
                notef.EndTime = wallTime;
                notef.Duration = notef.EndTime - notef.StartTime;
                //notef.Duration = (float)files.GetNoteDurationInMilliseconds(notef);
                ActiveNotes.Remove(note.Value);
                files.Notes.Add(notef);
                return false;
            }
        }
        return true;
    }
    public MidiEvent? ReadNextEvent(bool FastRead = false)
    {
        if (reader.BaseStream.Position >= stoppoint)
        {
            endOfTrack = true;
            return null;
        }
        if (endOfTrack)
        {
            return null;
        }
        uint statusTimeDelta = ReadValue();
        wallTime += statusTimeDelta;
        byte status = reader.ReadByte();

        if (status < 0x80)
        {
            status = previousStatus;
            reader.BaseStream.Position -= 1;
        }

        if ((status & 0xF0) == (byte)MidiFile.EventName.VoiceNoteOff)
        {
            previousStatus = status;
            byte channel = (byte)(status & 0x0F);
            byte noteID = reader.ReadByte();
            byte noteVelocity = reader.ReadByte();
            MaxNote = Math.Max(MaxNote, noteID);
            MinNote = Math.Min(MinNote, noteID);
            if (FastRead)
                return null;
            return (new MidiEvent(MidiEvent.EventType.NoteOff, noteID, noteVelocity, statusTimeDelta, channel ));
        }
        else if ((status & 0xF0) == (byte)MidiFile.EventName.VoiceNoteOn)
        {
            previousStatus = status;
            byte channel = (byte)(status & 0x0F);
            byte noteID = reader.ReadByte();
            byte noteVelocity = reader.ReadByte();
            if (FastRead)
                return null;
            if (noteVelocity == 0)
                return (new MidiEvent(MidiEvent.EventType.NoteOff, noteID, noteVelocity, statusTimeDelta, channel));
            else
                return (new MidiEvent(MidiEvent.EventType.NoteOn, noteID, noteVelocity, statusTimeDelta, channel) );
        }
        else if ((status & 0xF0) == (byte)MidiFile.EventName.VoiceAftertouch)
        {
            previousStatus = status;
            byte channel = (byte)(status & 0x0F);
            byte noteID = reader.ReadByte();
            byte noteVelocity = reader.ReadByte();
            if (FastRead)
                return null;
            return (new MidiEvent (MidiEvent.EventType.Other, noteID, noteVelocity, statusTimeDelta, channel));
        }
        else if ((status & 0xF0) == (byte)MidiFile.EventName.VoiceControlChange)
        {
            previousStatus = status;
            byte channel = (byte)(status & 0x0F);
            byte controlID = reader.ReadByte();
            byte controlValue = reader.ReadByte();
            if (FastRead)
                return null;
            return (new MidiEvent(MidiEvent.EventType.Other, controlID, controlValue, statusTimeDelta, channel));
        }
        else if ((status & 0xF0) == (byte)MidiFile.EventName.VoiceProgramChange)
        {
            previousStatus = status;
            byte channel = (byte)(status & 0x0F);
            byte programID = reader.ReadByte();
            if (FastRead)
                return null;
            return (new MidiEvent(MidiEvent.EventType.Other, programID, 0, statusTimeDelta, channel));
        }
        else if ((status & 0xF0) == (byte)MidiFile.EventName.VoiceChannelPressure)
        {
            previousStatus = status;
            byte channel = (byte)(status & 0x0F);
            byte channelPressure = reader.ReadByte();
            if (FastRead)
                return null;
            return (new MidiEvent (MidiEvent.EventType.Other ,0 ,0 ,  statusTimeDelta, channel));
        }
        else if ((status & 0xF0) == (byte)MidiFile.EventName.VoicePitchBend)
        {
            previousStatus = status;
            byte channel = (byte)(status & 0x0F);
            byte lsb = reader.ReadByte();
            byte msb = reader.ReadByte();
            if (FastRead)
                return null;
            return (new MidiEvent (MidiEvent.EventType.Other, 0, 0, statusTimeDelta, channel));
        }
        else if ((status & 0xF0) == (byte)MidiFile.EventName.SystemExclusive)
        {
            previousStatus = 0;

            if (status == 0xFF)
            {
                byte type = reader.ReadByte();
                uint length = ReadValue();

                switch (type)
                {
                    case (byte)MidiFile.MetaEventName.MetaSequence:
                        reader.ReadByte();
                        reader.ReadByte();
                        break;
                    case (byte)MidiFile.MetaEventName.MetaText:
                        reader.BaseStream.Position += length;
                        //ReadString(length);
                        break;
                    case (byte)MidiFile.MetaEventName.MetaCopyright:
                        reader.BaseStream.Position += length;
                        //ReadString(length);
                        break;
                    case (byte)MidiFile.MetaEventName.MetaTrackName:
                        Name = ReadString(length);
                        break;
                    case (byte)MidiFile.MetaEventName.MetaInstrumentName:
                        Instrument = ReadString(length);
                        break;
                    case (byte)MidiFile.MetaEventName.MetaLyrics:
                        reader.BaseStream.Position += length;
                        //ReadString(length);
                        break;
                    case (byte)MidiFile.MetaEventName.MetaMarker:
                        reader.BaseStream.Position += length;
                        //ReadString(length);
                        break;
                    case (byte)MidiFile.MetaEventName.MetaCuePoint:
                        reader.BaseStream.Position += length;
                        //ReadString(length);
                        break;
                    case (byte)MidiFile.MetaEventName.MetaChannelPrefix:
                        reader.ReadByte();
                        break;
                    case (byte)MidiFile.MetaEventName.MetaEndOfTrack:
                        endOfTrack = true;
                        break;
                    case (byte)MidiFile.MetaEventName.MetaSetTempo:
                        if (files.Tempo == 0)
                        {
                            if (FastRead)
                            {
                                files.Tempo = (uint)(reader.ReadByte() << 16);
                                files.Tempo |= (uint)(reader.ReadByte() << 8);
                                files.Tempo |= (uint)(reader.ReadByte() << 0);
                                files.BPM = 60000000 / files.Tempo;
                            }
                            else
                            {
                                reader.BaseStream.Position += 3;
                            }
                            //Console.WriteLine($"Tempo: {Tempo} ({BPM}bpm)");
                        }
                        else
                        {
                            if (FastRead)
                            {
                                var tempT = (uint)(reader.ReadByte() << 16);
                                tempT |= (uint)(reader.ReadByte() << 8);
                                tempT |= (uint)(reader.ReadByte() << 0);

                                files.TempoChanges.Add(new()
                                {
                                    Tempo = tempT,
                                    Ticks = wallTime,
                                    BPM = 60000000 / tempT
                                });
                            }
                            else
                            {
                                reader.BaseStream.Position += 3;
                            }
                        }
                        break;
                    case (byte)MidiFile.MetaEventName.MetaSMPTEOffset:
                        reader.ReadByte();
                        reader.ReadByte();
                        reader.ReadByte();
                        reader.ReadByte();
                        reader.ReadByte();
                        break;
                    case (byte)MidiFile.MetaEventName.MetaTimeSignature:
                        reader.ReadByte();
                        reader.ReadByte();
                        reader.ReadByte();
                        reader.ReadByte();
                        break;
                    case (byte)MidiFile.MetaEventName.MetaKeySignature:
                        reader.ReadByte();
                        reader.ReadByte();
                        break;
                    case (byte)MidiFile.MetaEventName.MetaSequencerSpecific:
                        reader.BaseStream.Position += length;
                        //ReadString(length);
                        break;
                    default:
                        Console.WriteLine($"Unrecognised MetaEvent: {type}");
                        break;
                }
            }
            else if (status == 0xF0)
            {
                reader.BaseStream.Position += ReadValue();
                //ReadString(ReadValue());
            }
            else if (status == 0xF7)
            {
                reader.BaseStream.Position += ReadValue();
                //ReadString(ReadValue());
            }
        }
        else
        {
            Console.WriteLine($"Unrecognised Status Byte: {status}");
        }

        return null;
    }
    public void Read()
    {
        _reader = new(File.OpenRead(fileName));
        _reader.BaseStream.Position = position;
        uint trackID = Swap32(reader.ReadUInt32());
        uint trackLength = Swap32(reader.ReadUInt32());
        stoppoint = position + trackLength;
        while (true)
        {
            if (endOfTrack)
            {
                break;
            }
            ReadNextEvent(true);
        }

        maxwallTime = wallTime;
        wallTime = 0;
        endOfTrack = false;

        _reader.BaseStream.Position = position + 8;
    }
}

public class MidiFile
{
    public enum EventName : byte
    {
        VoiceNoteOff = 0x80,
        VoiceNoteOn = 0x90,
        VoiceAftertouch = 0xA0,
        VoiceControlChange = 0xB0,
        VoiceProgramChange = 0xC0,
        VoiceChannelPressure = 0xD0,
        VoicePitchBend = 0xE0,
        SystemExclusive = 0xF0,
    }

    public enum MetaEventName : byte
    {
        MetaSequence = 0x00,
        MetaText = 0x01,
        MetaCopyright = 0x02,
        MetaTrackName = 0x03,
        MetaInstrumentName = 0x04,
        MetaLyrics = 0x05,
        MetaMarker = 0x06,
        MetaCuePoint = 0x07,
        MetaChannelPrefix = 0x20,
        MetaEndOfTrack = 0x2F,
        MetaSetTempo = 0x51,
        MetaSMPTEOffset = 0x54,
        MetaTimeSignature = 0x58,
        MetaKeySignature = 0x59,
        MetaSequencerSpecific = 0x7F,
    }

    public MidiFile() { }

    public MidiFile(string fileName)
    {
        ParseFile(fileName).Wait();
    }

    public void Clear()
    {
        Tracks.Unlink();
        Tempo = 0;
        BPM = 0;
        PPQ = 0;
    }

    public async Task<bool> ParseFile(string fileName)
    {
        using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            // Helper Utilities

            uint Swap32(uint value)
            {
                return ((value >> 24) & 0xff) | ((value << 8) & 0xff0000) | ((value >> 8) & 0xff00) |
                       ((value << 24) & 0xff000000);
            }

            ushort Swap16(ushort value)
            {
                return (ushort)((value >> 8) | (value << 8));
            }

            // Read MIDI Header
            uint fileID = Swap32(reader.ReadUInt32());
            uint headerLength = Swap32(reader.ReadUInt32());
            ushort format = Swap16(reader.ReadUInt16());
            ushort trackChunks = Swap16(reader.ReadUInt16());
            ushort division = Swap16(reader.ReadUInt16());
            if ((division & 0x8000) != 0) // Si es negativo, es SMPTE
            {
                sbyte fps = (sbyte)((division >> 8) & 0xFF); // FPS (usualmente -24, -25, -29, -30)
                byte subFrameResolution = (byte)(division & 0xFF); // Resolución de subframes

                PPQ = (uint)(Math.Abs(fps) * subFrameResolution); // Conversión básica
            }
            else
            {
                PPQ = division; // Ya es PPQ
            }
            for (ushort chunk = 0; chunk < trackChunks; chunk++)
            {
                uint trackID = Swap32(reader.ReadUInt32());
                if (trackID != 0x4D54726B) // "MTrk"
                {
                    Console.WriteLine($"Invalid chunk {chunk}, continuing...");
                    continue; // Saltar a la siguiente iteración
                }

                uint trackLength = Swap32(reader.ReadUInt32());
                Console.WriteLine($"Chunk {chunk:N0}/{trackChunks:N0}");
                Tracks.Add(new MidiTrack(fileName, reader.BaseStream.Position - 8, this, chunk));
    
                // Saltar la longitud del track
                reader.BaseStream.Position += trackLength;
            }

            Console.WriteLine($"Reading chunks...");
            /*await Parallel.ForEachAsync(Tracks, PublicObjects.opts, (track, token) =>
            {
                track.Read();
                return ValueTask.CompletedTask;
            });*/
            uint tc = 1;
            foreach (var track in Tracks)
            {
                Console.WriteLine($"Chunk read: {tc++:N0}/{trackChunks:N0}");
                track.Read();
            }
            // Convert Time Events to Notes
            uint maxWallTime = 0; // Tiempo máximo en ticks
            foreach (var track in Tracks)
            {
                maxWallTime = Math.Max(track.maxwallTime, maxWallTime);
                MaxKey = Math.Max(track.MaxNote, MaxKey);
                MinKey = Math.Min(track.MinNote, MinKey);
            }/*

            foreach (var track in Tracks)
            {
                uint wallTime = 0;
                List<MidiNote> activeNotes = new(128); // Preasignar capacidad
                uint sd2 = 0;
                foreach (var evt in Events)
                {
                    sd2 += evt.DeltaTick;
                    wallTime += evt.DeltaTick;
                    if (evt.Event == MidiEvent.EventType.NoteOn)
                    {
                        MinKey = Math.Min(evt.Key, MinKey);
                        MaxKey = Math.Max(evt.Key, MaxKey);
                        activeNotes.Add(new MidiNote
                            { Key = evt.Key, Velocity = evt.Velocity, StartTime = wallTime, Duration = 0, Channel = evt.Channel});
                    }

                    if (evt.Event == MidiEvent.EventType.NoteOff)
                    {
                        for (int i = 0; i < activeNotes.Count; i++)
                        {
                            var n = activeNotes[i];
                            if (n.Key == evt.Key && n.Channel == evt.Channel)
                            {
                                var note = activeNotes[i];
                                note.EndTime = wallTime;
                                note.Duration = (float)GetNoteDurationInMilliseconds(note);
                                note.Track = tc;
                                Notes.Add(note);
                                track.MinNote = Math.Min(track.MinNote, note.Key);
                                track.MaxNote = Math.Max(track.MaxNote, note.Key);
                                activeNotes.RemoveAt(i); // Remueve la nota activada
                                break; // Salir del bucle al encontrar la nota correspondiente
                            }
                        }
                    }
                }

                maxWallTime = Math.Max(sd2, maxWallTime);
                tc++;
            }*/

            TotalTicks = maxWallTime;/*
            StringBuilder sb = new();
            sb.AppendLine($"TotalTicks: {TotalTicks}");
            sb.AppendLine($"PPQ: {PPQ}");
            sb.AppendLine($"Tempo inicial: {Tempo}");
            foreach (var tempo in TempoChanges)
            {
                sb.AppendLine($"Cambio de tempo en {tempo.Ticks} ticks: {tempo.Tempo} microsegundos por negra");
            }*/
        }

        return true;
    }

    public void ParseUpTo(double start, double end)
    {
        Notes.RemoveAll(note =>
        {
            return note.EndTime < start;
        });
        foreach (var track in Tracks)
        {
            track.ParseUpTo(end);
        }
    }
    public double ConvertTicksToSeconds(uint ticks)
    {
        if (TempoChanges.ZeroLen) // Si no hay cambios de tempo
        {
            // Fórmula básica: (ticks / PPQ) * (Tempo / 1,000,000) → segundos
            return (double)ticks / PPQ * (Tempo / 1000000.0) * 1000;
        }

        double timeInSeconds = 0;
        uint previousTicks = 0;
        uint previousTempo = Tempo; // Tempo inicial (microsegundos por negra)

        // Iterar sobre los cambios de tempo para calcular segmentos
        foreach (var change in TempoChanges)
        {
            if (change.Ticks > ticks) break; // Si el cambio de tempo está después del tick objetivo, terminar

            // Calcular duración del segmento actual (desde previousTicks hasta change.Ticks)
            uint segmentTicks = change.Ticks - previousTicks;
            timeInSeconds += (double)segmentTicks / PPQ * (previousTempo / 1000000.0);

            // Actualizar para el siguiente segmento
            previousTicks = change.Ticks;
            previousTempo = change.Tempo;
        }

        // Calcular el último segmento (desde el último cambio de tempo hasta el tick objetivo)
        uint remainingTicks = ticks - previousTicks;
        timeInSeconds += (double)remainingTicks / PPQ * (previousTempo / 1000000.0);

        return timeInSeconds * 1000;
    }
    public double GetDurationInMilliseconds()
    {
        if (TempoChanges.ZeroLen) {
            // Si no hay cambios de tempo, usar el cálculo simple
            return (double)TotalTicks / PPQ * (Tempo / 1000000.0) * 1000;
        }

        double durationInSeconds = 0;
        uint previousTicks = 0;
        uint previousTempo = TempoChanges.First.Tempo;

        foreach (var change in TempoChanges) {
            // Calcular la duración del segmento actual
            uint ticksInSegment = change.Ticks - previousTicks;
            double segmentDuration = (double)ticksInSegment / PPQ * (previousTempo / 1000000.0);
            durationInSeconds += segmentDuration;

            // Actualizar valores para el siguiente segmento
            previousTicks = change.Ticks; 
            previousTempo = change.Tempo;
        }

        // Calcular la duración del último segmento (desde el último cambio de tempo hasta el final)
        uint ticksInLastSegment = (uint)(TotalTicks - previousTicks);
        double lastSegmentDuration = (double)ticksInLastSegment / PPQ * (previousTempo / 1000000.0);
        durationInSeconds += lastSegmentDuration;

        // Convertir a milisegundos
        return durationInSeconds * 1000;
    }
    public double GetNoteDurationInMilliseconds(MidiNote note)
    {
        if (TempoChanges.ZeroLen)
        {
            // Si no hay cambios de tempo, usar el cálculo simple
            return (double)note.Duration / PPQ * (Tempo / 1000000.0) * 1000;
        }

        double durationInSeconds = 0;
        uint previousTicks = note.StartTime;
        uint previousTempo = Tempo;

        foreach (var change in TempoChanges)
        {
            if (change.Ticks > note.StartTime && change.Ticks < note.EndTime)
            {
                // Calcular la duración del segmento actual
                uint ticksInSegment = change.Ticks - previousTicks;
                double segmentDuration = (double)ticksInSegment / PPQ * (previousTempo / 1000000.0);
                durationInSeconds += segmentDuration;

                // Actualizar valores para el siguiente segmento
                previousTicks = change.Ticks;
                previousTempo = change.Tempo;
            }
        }

        // Calcular la duración del último segmento (desde el último cambio de tempo hasta el final)
        uint ticksInLastSegment = note.EndTime - previousTicks;
        double lastSegmentDuration = (double)ticksInLastSegment / PPQ * (previousTempo / 1000000.0);
        durationInSeconds += lastSegmentDuration;

        // Convertir a milisegundos
        return durationInSeconds * 1000;
    }

    public List<MidiNote> Notes = new();
    public FastList<MidiTempoChange> TempoChanges = new();
    public FastList<MidiTrack> Tracks = new();
    public uint Tempo = 0;
    public byte MinKey = 255;
    public byte MaxKey = 0;
    
    public FastList<MidiEvent> Events = new();
    public uint BPM = 0;
    public uint TotalTicks = 0;
    public uint PPQ = 0;
}