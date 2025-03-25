# 🎹 MFR - Midi Free Renderer

[![GitHub Release](https://img.shields.io/github/v/release/Risc-A2/MFR?style=flat-square)](https://github.com/TuUsuario/MFR/releases)
[![Build Status](https://img.shields.io/github/actions/workflow/status/Risc-A2/MFR/dotnet.yml?branch=main&style=flat-square)](https://github.com/TuUsuario/MFR/actions)
[![License](https://img.shields.io/badge/license-GPL-blue?style=flat-square)](LICENSE)

![MFR Demo](https://i.imgur.com/JQZ1KlP.png)  
*Renderizado de notas MIDI con sombras degradadas (ejemplo visual)*

---

## 🚀 Características
- **Renderizado eficiente** de archivos MIDI a video (MP4) usando **SkiaSharp** y **FFmpeg**.
- **Efectos visuales**:
  - Sombras degradadas (estilo *Piano From Above*).
  - Colores HSL por pista.
  - Teclas de piano interactivas.
- **Multiplataforma**: Windows, Linux, macOS.
- **Optimizado para grandes archivos** (ej: *"Freedom Dive"* con 15k+ notas).

---

## 📦 Instalación
### Requisitos
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [FFmpeg](https://ffmpeg.org/download.html) (añadido al PATH).

### Ejecución
```bash
git clone https://github.com/TuUsuario/MFR.git
cd MFR
dotnet run --project MFR.csproj -- "ruta/a/tu/archivo.mid"
```
