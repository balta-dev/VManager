# Contribuir a VManager

Primero que nada, ¡quisiera agradecerte por estar leyendo esto y querer apoyar este proyecto! <3

## Abrir un Issue

Cualquier bug o sugerencia debería ser reportada en [balta-dev/VManager/issues](https://github.com/balta-dev/VManager/issues). 

Por defecto la salida de logs está deshabilitada, por lo que tendrás que activarla (Configuración -> Avanzado), cerrar VManager y repetir el procedimiento que genera problemas.

Por favor, incluye el log (Configuración -> Avanzado -> Abrir carpeta de logs) de la última vez que abriste VManager. Copia todo desde `[DEBUG]: LOG HABILITADO.` hasta el final del archivo y pegalo en el cuerpo del issue envuelto con ``` para un mejor formato. Debería verse algo así:

   ```text
   [DEBUG]: LOG HABILITADO.
   [STARTUP] [188ms] StreamWriter setup (delta: 67ms)
   [DEBUG]: Iniciando VManager...
   [STARTUP] [269ms] FFmpegManager.Initialize() retornó (delta: 145ms)
   [STARTUP] [344ms] YtDlpManager.Initialize() retornó (delta: 74ms)
   [STARTUP] [348ms] DenoManager.Initialize() retornó (delta: 4ms)
   [STARTUP] [348ms] Arrancando Avalonia...
   [DENO] path: /tmp/deno
   [FFMPEG] ffmpeg: /usr/bin/ffmpeg
   [FFMPEG] ffprobe: /usr/bin/ffprobe
   [STARTUP] [998ms] OnFrameworkInitializationCompleted
   [STARTUP] [1034ms] Config cargada
   [STARTUP] [1036ms] Tema aplicado
   Idioma 'es' cargado con 316 entradas
   [YTDLP] path: /tmp/yt-dlp
   [STARTUP] [1277ms] MainWindowViewModel creado
   [GetSystemAccentColor] Usando color de sistema: #ff0078d7
   [STARTUP] [1692ms] MainWindow creada
   [GetSystemAccentColor] Usando color custom: #ffff11c3
   [DEBUG]: Startup en 1900 ms
   Buscando actualizaciones...
   Version cacheada: 1.7.6
   Version de Assembly: 1.7.6.0
   [GetSystemAccentColor] Usando color custom: #ffff11c3
   [INFO] [APLAY] Sonido reproducido: dummy.wav
   ...
   ```

**El log debe ser posteado en formato texto; ÚNICAMENTE en modo texto.**

La salida contiene información muy importante de depuración. Si este texto falta, la reproducción del problema se vuelve mucho más complicada y no puede ser tratado. Que el log deba estar en formato texto no significa que no puedas mostrar capturas de pantalla si el problema es de naturaleza visual (estética o funcional).

Por favor, cuando termines de escribir tu issue reléelo para evitar errores (podrías y deberías seguir esto como lista de verificación):

#### ¿La descripción misma del problema es suficiente?

Si alguien más leyera tu problema debería ser capaz de entenderlo, sin mucho ida y vuelta. Por favor, elabora qué feature pides, o qué bug quieres que se corrija. Asegúrate de que sea obvio

* Cual es el problema
* Como podría ser resuelto
* Como se vería tu solución propuesta

#### ¿Estás usando la última versión?

Antes de reportar un problema, ejecuta `Updater`. Si no hay actualizaciones pendientes, puedes continuar con tu reporte.

#### ¿Es un issue ya documentado?

Asegúrate que no sea un issue que otra persona ya haya abierto. Si existe, suscríbete para ser notificado cuando haya algún progreso. A menos que tengas algo nuevo para aportar a la conversación, no comentes.

## Instrucciones para Desarrolladores

Si deseas buildear VManager, se espera que ya hayas leído (y entiendas) como [compilar el proyecto](README.md#compilar) por tu cuenta.

Antes de que empieces a escribir código para implementar una nueva feature, abre un issue explicando la razón de ser del feature y al menos un caso de uso. No es obligatorio, pero si es deseable.

### Agregar una nueva feature

1. [Hace fork del repositorio](https://github.com/balta-dev/VManager/fork).
2. Clona tu propio repositorio
   ```bash
   git clone git@github.com:YOUR_GITHUB_USERNAME/VManager.git
   ```
3. Crea una nueva git branch con
   ```bash
   cd VManager
   git checkout -b feature/TU_FEATURE
   ```
4. Implementa tu solución, es decir, escribe tu código y asegúrate de que `dotnet test` no arroje nuevas fallas.
5.  [Crea un pull request](https://help.github.com/articles/creating-a-pull-request). Se revisará el código y se mergeará. En tal caso, ¡muchas gracias por contribuir!

###  Traducciones

Puedes encontrar las localizaciones en [VManager/Assets/Localization/](VManager/Assets/Localization/). Actualmente VManager tiene traducción a 14 idiomas distintos con la ayuda de IA, por lo que podrían ser inexactas o requerir cambios.

Ya que están parseados en formato JSON, no necesitas saber C# para contribuir a la traducción.

Si deseas agregar un nuevo idioma, tendrás que agregarlo en [VManager/ViewModels/ConfigurationViewModel.cs](VManager/ViewModels/ConfigurationViewModel.cs)... tanto en la colección observable `Idiomas`

```c#
public ObservableCollection<string> Idiomas { get; } = new()
        {
            "English",
            "Español",
        	...
        //  "Otro idioma"
        };
```

Como en el setter de `IdiomaSeleccionado` 

````c#
switch (value)
                {
                    case "English": LocalizationService.Instance.CurrentLanguage = "en"; break;
                    case "Español": LocalizationService.Instance.CurrentLanguage = "es"; break;
        		 	...
                 // case "Otro idioma": LocalizationService.Instance.CurrentLanguage = "otroidioma"; break;
                }
````

### Tests

El proyecto cuenta con tests unitarios y de integración clasificados por tipo dentro de `VManager.Tests/`. Para correrlos:

```
dotnet test
```

> **Importante:** Algunos tests unitarios existentes están desactualizados y no representan el comportamiento real del código. Si un test falla, verificá primero si el problema es el test en sí antes de asumir que tu cambio rompió algo.

Los tests de integración relacionados al [issue #3](https://github.com/balta-dev/VManager/issues/3) fallan intencionalmente (`assert failed`) ya que cubren funcionalidad aún no implementada. Esto es esperado y no debe ser corregido hasta que dicho issue sea resuelto.

### Updater

`Updater/` contiene el proyecto auxiliar encargado de mantener VManager actualizado. **No está abierto a nuevas features ni cambios de comportamiento**, únicamente se aceptan correcciones de bugs críticos. Si encontrás un problema, abrí un issue antes de mandar un PR.
