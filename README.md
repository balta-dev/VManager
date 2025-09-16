# VManager 🎬

Una herramienta **fácil de usar** y **rápida** para gestionar videos: recortar, comprimir, cambiar formato y más.

## Descripción

VManager nace de la evolución de dos herramientas que desarrollé para uso en terminal ([`vcut`](https://github.com/balta-dev/vcut) y [`vcompr`](https://github.com/balta-dev/vcompr)), con el objetivo de facilitar aún más su uso general y hacer estas funcionalidades accesibles para el usuario promedio. 

### ✨ Características principales

- 🎥 **Recortar videos** - Extrae segmentos específicos de tus videos
- 🗜️ **Comprimir videos** - Reduce el tamaño de archivo manteniendo calidad
- 🔄 **Cambiar formato** - Convierte entre diferentes formatos de video
- 🖱️ **Interfaz gráfica intuitiva** - Arrastra y suelta archivos
- ⚡ **Rápido y eficiente** - Construido sobre FFmpeg
- 🌐 **Multiplataforma** - Windows, Linux y macOS

## 💻 Tecnologías

- **.NET 9** - Framework principal
- **Avalonia UI** - Interfaz de usuario multiplataforma  
- **ReactiveUI** - Arquitectura MVVM reactiva
- **FFmpeg** - Motor de procesamiento de video

## 📥 Instalación y uso

### Windows 🪟
1. Descarga el archivo `VManager-win-x64.zip` desde [Releases](../../releases)
2. Extrae el contenido
3. Ejecuta `VManager.exe`
4. ¡Listo para usar! ✅

### Linux 🐧
1. Descarga el archivo `VManager-linux-x64.tar.gz` desde [Releases](../../releases)
2. Extrae el contenido:
   ```bash
   tar -xzf VManager-linux-x64.tar.gz
   ```
3. Ejecuta la aplicación:
   ```bash
   ./VManager
   ```

> **⚠️ Nota**: Para Linux, Avalonia no tiene una implementación **Drag & Drop**, así que la única forma es ocupando el botón "Examinar". A pesar de que los desarrolladores de Avalonia no han tenido mucho interés, actualmente hay varios issues que están intentando resolver este problema (ejemplo: issue [#19232](https://github.com/AvaloniaUI/Avalonia/pull/19232) busca mergearse tras solucionar issue [#19347](https://github.com/AvaloniaUI/Avalonia/pull/19347)). El objetivo es utilizar la implementación original de Avalonia, pero si no se resuelve en un tiempo razonable se implementará de manera provisoria un GTK Helper para que pueda capturar el evento y se lo comunique a Avalonia mediante IPC.

### macOS 🍎
1. Descarga el archivo `VManager-osx-x64.tar.gz` desde [Releases](../../releases)
2. Extrae el contenido
3. **Instala FFmpeg** manualmente:
   ```bash
   brew install ffmpeg
   ```
4. Ejecuta la aplicación

> **⚠️ Nota**: En macOS es necesario instalar FFmpeg por separado usando Homebrew u otro gestor de paquetes (no fueron incluidos los binarios en el ejecutable). Todavía no ha sido testeado en esta plataforma, pero según la documentación de Avalonia la función **Drag & Drop** si está disponible.

## 🎯 Cómo usar

1. **Abre VManager**
2. Usa el botón "Examinar" o **arrastra tu video** al área correspondiente
3. **Comprueba las opciones** según lo que necesites hacer
4. **Procesa** y obtén tu video optimizado

![Demo](assets/demo.gif) <!-- Agregá un gif demo cuando tengas uno -->

## 🛠️ Desarrollo futuro

VManager está en **desarrollo activo**. Se están considerando agregar más herramientas para gestión de video y audio, incluyendo:

- 🎵 Extracción de audio
- 📊 Soporte a múltiples archivos simultáneamente
- Paridad de features multiplataforma (DnD)
- Y mucho más...

## 🤝 Contribuciones

Las contribuciones son bienvenidas. Si tenés ideas, reportes de bugs o mejoras, no dudes en:

- Abrir un [Issue](../../issues)
- Enviar un [Pull Request](../../pulls)
- Sugerir nuevas características

## 📄 Licencia

Este proyecto está bajo la licencia [MIT](LICENSE.md).

## 🙏 Reconocimientos

- **FFmpeg** - El corazón del procesamiento de video
- **Avalonia** - Framework UI multiplataforma
- **ReactiveUI** - Arquitectura reactiva
- [**@femaa33**](https://www.youtube.com/@femaa33) - Por la idea de comenzar este proyecto ♡

---

**¿Te resulta útil VManager?** ⭐ ¡Dale una estrella al repo!
