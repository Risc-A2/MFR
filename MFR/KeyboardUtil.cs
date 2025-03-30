namespace MFR;
using SkiaSharp;
using System.Collections.Generic;

public class MidiNoteRenderer
{
    public readonly int _midiNoteMin;
    public readonly int _midiNoteMax;
    private readonly float _whiteKeyHeight;
    private readonly float _blackKeyHeight;
    private readonly float _blackKeyWidthRatio;
    private readonly float _yOffset;

    public MidiNoteRenderer(
        int midiNoteMin = 0,   // A0 (nota más grave en piano estándar)
        int midiNoteMax = 128,  // C8 (nota más aguda)
        float whiteKeyHeight = 100f,
        float blackKeyHeightRatio = 0.6f,
        float blackKeyWidthRatio = 0.6f,
        float yOffset = 0f)
    {
        _midiNoteMin = midiNoteMin;
        _midiNoteMax = midiNoteMax;
        _whiteKeyHeight = whiteKeyHeight;
        _blackKeyHeight = whiteKeyHeight * blackKeyHeightRatio;
        _blackKeyWidthRatio = blackKeyWidthRatio;
        _yOffset = yOffset;
    }

    public (Dictionary<int, SKRect> KeyRects, float[] WhiteKeyCenters) GenerateKeys(int displayWidth)
    {
        var keyRects = new Dictionary<int, SKRect>(); // Mapeo MIDI → Rectángulo
        var whiteKeyCenters = new List<float>();

        bool[] isBlackKeyInOctave = { false, true, false, true, false, false, true, false, true, false, true, false };
        int totalWhiteKeys = CountWhiteKeys(_midiNoteMin, _midiNoteMax);
        float whiteKeyWidth = displayWidth / (float)totalWhiteKeys;

        int whiteKeyIndex = 0;
        for (int midiNote = _midiNoteMin; midiNote <= _midiNoteMax; midiNote++)
        {
            int noteInOctave = (midiNote - _midiNoteMin) % 12;
            bool isBlack = isBlackKeyInOctave[noteInOctave];
            float xPos = whiteKeyIndex * whiteKeyWidth;

            if (!isBlack)
            {
                // Tecla blanca
                var rect = new SKRect(xPos, _yOffset, xPos + whiteKeyWidth, _yOffset + _whiteKeyHeight);
                keyRects.Add(midiNote, rect);
                whiteKeyCenters.Add(rect.MidX);
                whiteKeyIndex++;
            }
            else
            {
                // Tecla negra (centrada entre blancas)
                float blackKeyWidth = whiteKeyWidth * _blackKeyWidthRatio;
                var rect = new SKRect(
                    xPos - (blackKeyWidth / 2),
                    _yOffset,
                    xPos + (blackKeyWidth / 2),
                    _yOffset + _blackKeyHeight
                );
                keyRects.Add(midiNote, rect);
            }
        }

        return (keyRects, whiteKeyCenters.ToArray());
    }

    private int CountWhiteKeys(int minNote, int maxNote)
    {
        int whiteKeys = 0;
        bool[] isBlackKeyInOctave = { false, true, false, true, false, false, true, false, true, false, true, false };

        for (int note = minNote; note <= maxNote; note++)
        {
            int noteInOctave = note % 12;
            if (!isBlackKeyInOctave[noteInOctave]) whiteKeys++;
        }
        return whiteKeys;
    }
}