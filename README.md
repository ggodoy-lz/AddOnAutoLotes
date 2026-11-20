# AddOnAutoLotes — Autoasignación Masiva de Lotes y Series para SAP Business One

Add-on nativo para **SAP Business One 10.0 (x64)** que agrega el botón **"Autoasignar Todo"** en la ventana de selección de números de lote (Formulario 42) y números de serie (Formulario 41).

Resuelve el problema de tener que hacer clic fila por fila en documentos con múltiples líneas (Entregas, Facturas de Deudores, Traslados de Inventario, Salidas de Mercancías, etc.), asignando todos los SKUs en **1 a 2 segundos**.

---

## 🚀 ¿Cómo funciona?

1. El usuario crea o abre cualquier documento de salida en SAP (ej. Entrega, Factura o Traslado) con 50 o más SKUs gestionados por lotes.
2. Al presionar **Crear** o abrir la gestión de lotes, SAP abre la ventana modal estándar: **"Selección de números de lote"** (Formulario 42).
3. En esa ventana, al lado del botón estándar de *Asignación automática*, aparece el nuevo botón: **[Autoasignar Todo]**.
4. Al hacer un solo clic:
   - Recorre en bucle todas las líneas de la matriz superior (Item 3).
   - En cada línea activa la fila y ejecuta la auto-selección nativa de SAP respetando el stock disponible de cada lote.
   - Notifica en la barra de estado de SAP: `"¡Lotes asignados en X de X líneas! Confirmando..."`.
   - Presiona automáticamente el botón **Actualizar / OK** (Item 1) para guardar y cerrar la ventana modal, dejando el documento listo para crear.

---

## 📦 Archivos generados

| Archivo / Carpeta | Descripción |
| :--- | :--- |
| `AddOnAutoLotes.zip` | Paquete de extensión listo para instalar en SAP B1. |
| `AddOnAutoLotes.ard` | Archivo de registro del Add-on (con firma MD5). |
| `bin/x64/Release/AddOnAutoLotes.exe` | Ejecutable compilado en 64 bits. |
| `bin/x64/Release/AddOnAutoLotes.exe.config` | Archivo de configuración del Add-on. |
| `BatchAutoAssigner.cs` | Lógica de detección de formulario y bucle de asignación. |
| `Program.cs` | Conexión UI API con SAP B1, filtros de eventos y ciclo de vida. |
| `build.ps1` | Script automatizado de compilación y empaquetado. |
| `AddOnAutoLotes.sln` / `.csproj` | Solución de Visual Studio en C#. |

---

## 🛠️ Instalación en SAP Business One

### Método 1: Como Extensión / Paquete Ligero (Recomendado)
1. Abrir **SAP Business One** con usuario administrador (`manager`).
2. Ir a: **Gestión > Add-Ons > Gestión de extensiones** (o *Extension Manager* en el System Landscape Directory / SLD).
3. Cargar el archivo `AddOnAutoLotes.zip`.
4. Asignar la extensión a la sociedad (Base de datos de la empresa).
5. Configurar el inicio en **Automático** para los usuarios deseados.

### Método 2: Como Add-On Tradicional (Vía ARD)
1. En SAP B1, ir a: **Gestión > Add-Ons > Gestión de Add-Ons**.
2. En la solapa **Gestión de registro de Add-Ons**, hacer clic en **Registrar Add-On**.
3. Seleccionar el archivo `AddOnAutoLotes.ard` ubicado en esta carpeta.
4. En la solapa **Asignación de Add-Ons**, asignar el Add-on a la sociedad y establecer el modo de inicio en **Automático**.
5. Al reiniciar SAP B1 (o iniciar el Add-on desde el menú), el botón estará activo inmediatamente.

---

## ⚙️ Configuración (`AddOnAutoLotes.exe.config`)

Dentro del archivo `.config` se pueden ajustar los siguientes parámetros sin necesidad de recompilar:

```xml
<appSettings>
  <!-- Texto visible en el botón -->
  <add key="ButtonCaption" value="Autoasignar Todo" />
  
  <!-- Ancho del botón en píxeles -->
  <add key="ButtonWidth" value="140" />

  <!-- true = presiona 'Actualizar' (Item 1) automáticamente tras completar las filas -->
  <!-- false = deja la ventana abierta para revisión del usuario (Recomendado) -->
  <add key="AutoConfirm" value="false" />

  <!-- true = habilita el botón también en Números de Serie (Formulario 41) -->
  <add key="EnableSerials" value="true" />
</appSettings>
```

---

## 🔄 Recompilar

Si se requiere modificar el código en el futuro, basta con ejecutar en PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File .\AddOnAutoLotes\build.ps1
```
El script compilará el `.exe`, recalculará el hash MD5, actualizará el `.ard` y generará un nuevo `AddOnAutoLotes.zip`.
