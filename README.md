<img width="100" height="100" alt="VManager" src="https://github.com/user-attachments/assets/8ac28b60-aaca-4af4-8883-728adf8a9952" />ㅤㅤ



## VManager
### Una herramienta **fácil de usar** y **rápida** para gestionar videos: recortar, comprimir, cambiar formato y más.

VManager nace de la evolución de dos herramientas que desarrollé para uso en terminal ([`vcut`](https://github.com/balta-dev/vcut) y [`vcompr`](https://github.com/balta-dev/vcompr)), con el objetivo de facilitar aún más su uso general y hacer estas funcionalidades accesibles para el usuario promedio. 

<img width="897" height="662" alt="image" src="https://github.com/user-attachments/assets/640c21ed-501a-4b8f-ab14-924c05250e09" />
<img width="896" height="661" alt="image" src="https://github.com/user-attachments/assets/9375575f-1ffe-4d3e-8783-e7ed34b31fb5" />
<img width="897" height="664" alt="image" src="https://github.com/user-attachments/assets/3cea5900-2a38-45ea-a33f-f8b3df4cc6d4" />









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

---

### Windows 🪟
1. Descarga el archivo `VManager-win-x64.zip` desde [Releases](../../releases)
2. Extrae el contenido
3. Ejecuta `VManager.exe`
4. ¡Listo para usar! ✅

> **⚠️ Nota**: Si llegas a tener algún problema es muy probable que te falte .NET Framework, para solucionarlo descargá la versión "self-contained".

---

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

---

### macOS 🍎
1. Descarga el archivo `VManager-osx-x64.tar.gz` desde [Releases](../../releases)
2. Extrae el contenido
3. **Instala FFmpeg** manualmente:
   ```bash
   brew install ffmpeg
   ```
4. Ejecuta la aplicación

> **⚠️ Nota**: Todavía no ha sido testeado en esta plataforma.

---

## 🎯 Cómo usar

1. **Abre VManager**
2. Usa el botón "Examinar" o **arrastra tu video** al área correspondiente
3. **Comprueba las opciones** según lo que necesites hacer
4. **Procesa** y obtén tu video optimizado

![Demo](assets/demo.gif) <!-- Agregá un gif demo cuando tengas uno -->

## 🛠️ Desarrollo futuro

VManager está en **constante desarrollo**. Se están considerando agregar más herramientas para gestión de video y audio, incluyendo:

- 📊 Soporte a múltiples archivos simultáneamente
- Paridad de features multiplataforma (DnD)
- Y mucho más...

## 🤝 Contribuciones

Las contribuciones son bienvenidas y promovidas. Si tenés ideas, reportes de bugs o mejoras, no dudes en:

- a) Abrir un [Issue](../../issues)
- b) Enviar un [Pull Request](../../pulls)
- c) Sugerir nuevas características

## 📄 Licencia

Este proyecto está bajo la licencia [MIT](LICENSE.md).

## 🙏 Reconocimientos

- **FFmpeg** - El corazón del procesamiento de video
- **Avalonia** - Framework UI multiplataforma
- **ReactiveUI** - Arquitectura reactiva
- [**@femaa33**](https://www.youtube.com/@femaa33) - Por la idea de comenzar este proyecto ♡ Y POR EL LOGO

---

**¿Te resulta útil VManager?** ⭐ ¡Dale una estrella al repo!
