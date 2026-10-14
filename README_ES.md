# LG webOS Remote 📺📱

<p align="center">
  <img src="Resources/drawable/app_logo.png" width="96" height="96" alt="LG webOS Remote Logo" />
</p>

<p align="center">
  <b>Un control remoto moderno, rápido e intuitivo para Smart TVs LG webOS en Android.</b>
</p>

<p align="center">
  <a href="README.md">🇧🇷 Português</a> •
  <a href="README_EN.md">🇺🇸 English</a> •
  <a href="README_ES.md">🇪🇸 Español</a>
</p>

---

## 🌟 Descripción General

**LG webOS Remote** es una aplicación para Android desarrollada en **.NET 10** para permitir el control total de su Smart TV LG webOS directamente desde su smartphone a través de su red Wi-Fi local.

Funciona en cualquier hogar y con cualquier enrutador: solo se necesita que la Smart TV y el teléfono Android estén conectados a la misma red local. No requiere servidores en la nube ni registro de cuentas.

---

## 📸 Capturas de Pantalla

<div align="center">
  <table>
    <tr>
      <td align="center" width="33%">
        <b>Control Remoto</b><br/>
        <img src="screenshots/remote.jpg" width="220" alt="Pantalla de Control Remoto" />
      </td>
      <td align="center" width="33%">
        <b>Audio</b><br/>
        <img src="screenshots/audio.jpg" width="220" alt="Pantalla de Audio" />
      </td>
      <td align="center" width="33%">
        <b>Aplicaciones</b><br/>
        <img src="screenshots/apps.jpg" width="220" alt="Pantalla de Aplicaciones" />
      </td>
    </tr>
    <tr>
      <td align="center" width="33%">
        <b>Entradas (HDMI)</b><br/>
        <img src="screenshots/inputs.jpg" width="220" alt="Pantalla de Entradas" />
      </td>
      <td align="center" width="33%">
        <b>Ajustes de Imagen</b><br/>
        <img src="screenshots/picture.jpg" width="220" alt="Pantalla de Ajustes de Imagen" />
      </td>
      <td align="center" width="33%">
        <b>Ajustes y Conexión</b><br/>
        <img src="screenshots/settings.jpg" width="220" alt="Pantalla de Ajustes y Conexión" />
      </td>
    </tr>
  </table>
</div>

---

## ✨ Características

- 🕹️ **Control Remoto Completo**: D-Pad circular ergonómico, botón OK central, Inicio, Volver, Menú, Salir, Volumen (+/-), Canales (+/-) y Silencio.
- 🚀 **Accesos Directos a Streaming**: Acceso directo con un solo toque a **Netflix**, **YouTube**, **HBO Max** y **Prime Video**.
- 🔌 **Control de Energía**: Apague la TV desde la aplicación cuando esté conectada o reconecte con un toque.
- 🔊 **Control Preciso de Audio**: Barra deslizante de volumen de 0 a 100, indicador numérico en vivo y botón para Silenciar / Activar Sonido.
- 📱 **Catálogo de Aplicaciones en Orden Alfabético**: Todas las aplicaciones instaladas en la TV organizadas de la A a la Z con búsqueda en tiempo real.
- 📺 **Selector de Entradas**: Cambie fácilmente entre HDMI 1, HDMI 2, HDMI 3, AV y Componente.
- 🎨 **Calibración de Imagen**: Ajuste Luz de Fondo (Backlight), Brillo, Contraste y Color en tiempo real.
- 🔍 **Búsqueda Automática en Wi-Fi**: Escanea la red local mediante SSDP para encontrar TVs LG automáticamente.
- ✍️ **Configuración Manual**: Permite ingresar manualmente la dirección IPv4 de la TV en caso de que el router bloquee multicast.
- 🌐 **Soporte Multi-idioma**: Cambie dinámicamente entre **Español**, **Portugués** e **Inglés** desde la pestaña Ajustes.
- 🔒 **Conexión Segura y Persistente**: Utiliza el protocolo WebSocket oficial de webOS y guarda la clave de emparejamiento localmente.

---

## 🚀 Cómo Conectar a su TV

### 1. Requisitos Previos
1. Asegúrese de que su **Smart TV LG** esté encendida.
2. Asegúrese de que la TV y su smartphone Android estén conectados a la **misma red Wi-Fi**.

### 2. Emparejamiento y Conexión
1. Abra la aplicación **LG webOS Remote**.
2. Vaya a la pestaña **Ajustes** (icono de engranaje en la barra inferior).
3. Toque el botón **Buscar TVs LG en la Red Wi-Fi**:
   - Las TVs encontradas aparecerán en la lista. Toque sobre su TV para conectar.
   - *Alternativa Manual*: Si la búsqueda automática no encuentra su TV, escriba la dirección IPv4 de su TV en el campo **IPv4 de la TV** (ej: `192.168.1.100`) y toque **Guardar y Conectar**.
4. **Mire la pantalla de su TV LG**:
   - Aparecerá un mensaje solicitando autorización: *"LG webOS Remote solicita conexión"*.
   - Usando el control físico de la TV, seleccione **Permitir** / **Sí**.
5. La aplicación mostrará un popup de confirmación: **"¡Conectado con Éxito! 🎉"**.
6. ¡Listo! La clave queda guardada y ya puede usar su control con total libertad.

---

## 🌐 Cómo Cambiar el Idioma

1. Abra la aplicación y vaya a la pestaña **Ajustes**.
2. En la sección **Idioma de la Aplicación**, toque en:
   - 🇧🇷 **Português**
   - 🇺🇸 **English**
   - 🇪🇸 **Español**
3. La aplicación se recargará de inmediato en el idioma seleccionado y guardará su preferencia.

---

## 📥 Instalación (.APK)

1. Vaya a la sección [Releases](../../releases) de este repositorio en GitHub.
2. Descargue el archivo `LGwebOSRemote-Release.apk`.
3. Abra el archivo descargado en su dispositivo Android y permita la instalación de orígenes desconocidos si se le solicita.
4. Complete la instalación y comience a usar la aplicación.

---

## 🛠️ Tecnologías Empleadas

- **C# / .NET 10 Android**
- **Material Components for Android** (BottomNavigationView, Toolbar, Cards)
- **WebSockets (`ClientWebSocket`)** para comunicación bidireccional con LG webOS
- **SSDP / UDP Sockets** para descubrimiento en la red local
- **JSON System.Text.Json**

---

## 📄 Licencia

Distribuido bajo la Licencia MIT. Consulte el archivo `LICENSE` para más información.
