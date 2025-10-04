<img width="100" height="100" alt="VManager" src="https://github.com/user-attachments/assets/1035f7c9-dfe5-47d2-a21a-2c88e296e50b" />ㅤㅤ
ㅤㅤㅤㅤㅤㅤㅤㅤㅤㅤㅤ

## VManager
### Una herramienta **fácil de usar** y **rápida** para gestionar videos: recortar, comprimir, cambiar formato y más.

VManager nace de la evolución de dos herramientas que desarrollé para uso en terminal ([`vcut`](https://github.com/balta-dev/vcut) y [`vcompr`](https://github.com/balta-dev/vcompr)), con el objetivo de facilitar aún más su uso general y hacer estas funcionalidades accesibles para el usuario promedio. 

<img width="1281" height="834" alt="image" src="https://github.com/user-attachments/assets/188a6b69-3fde-4ca6-bd37-68f98c1456ea" />
<img width="1279" height="827" alt="image" src="https://github.com/user-attachments/assets/2cbca789-016b-452e-b53d-a088c5f780ab" />
<img width="1278" height="824" alt="image" src="https://github.com/user-attachments/assets/107d262d-a7fb-4544-8ab1-1546b029056c" />






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

> **⚠️ Nota**: Todavía no ha sido testeado en esta plataforma. También, según la documentación de Avalonia la función **Drag & Drop** si está disponible.

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
