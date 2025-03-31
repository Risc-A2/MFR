using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Platform.Storage;
using MFR_Core;
using SkiaSharp;

namespace MFR_GUI;

public partial class MainWindow : Window
{
	private async Task<string?> ShowFilePickerAsync()
	{
		// Obtener el TopLevel de la ventana actual
		var topLevel = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException("No hay ventana activa");

		// Configurar las opciones del diálogo
		var options = new FilePickerOpenOptions
		{
			Title = "Open MIDI File",
			FileTypeFilter = new[]
			{
				new FilePickerFileType("Archivos MIDI")
				{
					Patterns = ["*.mid", "*.midi"]
				}
			},
			AllowMultiple = false
		};

		// Mostrar el diálogo
		var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
		return files.Count > 0 ? files[0].Path.LocalPath : null;
	}
	private MidiPlayer? _player = null;
	public MainWindow()
	{
		InitializeComponent();
		SkiaView.Width = 1920;
		SkiaView.Height = 1080;
		SkiaView.PaintSurface += OnSkiaPaint;

		// Botón para cargar MIDI (sin XAML)
		loadButton.Click += async (s, e) => await LoadMidiFile();
		
		
	}
	private void OnSkiaPaint(object? sender, SKPaintSurfaceEventArgs e)
	{
		_player?.Render(e.Surface.Canvas, e.Surface);
	}

	private async Task LoadMidiFile()
	{
		// Implementa tu lógica de carga MIDI aquí
		var filePath = await ShowFilePickerAsync();
		if (filePath != null)
		{
			_player = new MidiPlayer(filePath, 60, (int)SkiaView.Width, (int)SkiaView.Height, ffmpegCheck.IsChecked.GetValueOrDefault(), ffmpegOutput.Text ?? "output.mp4");
			SkiaView.InvalidateSurface(); // Forzar redibujado
			Task.Run(async () =>
			{
				while (_player.midiTime < _player.dur)
				{
					await Task.Delay(16);
					SkiaView.InvalidateSurface();
				}
			});
		}
	}
}