# Integración de PLCSim Advanced con Unity mediante API REST

Puente de comunicación bidireccional entre PLCSim Advanced y Unity a través de una API REST intermediaria en C# (.NET Framework).

> `API/` — Proyecto C# (servidor)  
> `unity/` — Scripts de Unity listos para usar

---

## Inicio Rápido

### 1. Lanzar la API
Ejecuta el compilado de la API **como Administrador** en la máquina donde corre PLCSim Advanced.  
Si Unity y la API están en máquinas distintas, abre el puerto en el firewall:
```bash
netsh advfirewall firewall add rule name="API PLCSim Port 9000" dir=in action=allow protocol=TCP localport=9000
```

### 2. Configurar Unity
En `Edit > Project Settings > Player > Other Settings`, cambia `Allow downloads over HTTP` a `Always allowed`.

### 3. Añadir los scripts al proyecto
Copia los archivos de `unity/` a tu proyecto de Unity:
- `ApiInterface.cs` — Singleton que gestiona toda la comunicación con la API
- `ConectarseInstancia.cs` — Panel de UI para conectarse a instancias desde el editor

### 4. Ajusta `baseUrl` en el Inspector de `ApiInterface` a la IP y puerto de tu API.
---

## Referencia de Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/plc/instances` | Lista instancias disponibles |
| POST | `/api/plc/instances/{id}/connect` | Conecta a una instancia |
| GET | `/api/plc/tags` | Lista tags disponibles |
| GET | `/api/plc/tags-with-values` | Lista tags con su valor actual |
| GET | `/api/plc/tags/{tag}?type={type}` | Lee el valor de un tag |
| PUT | `/api/plc/tags/{tag}` | Escribe el valor de un tag |

---

## Uso de ApiInterface en Unity

`ApiInterface` es un Singleton accesible desde cualquier script mediante `ApiInterface.Instance`.  
Todas las llamadas son asíncronas: reciben un callback `onSuccess` y un `onError` opcional.

### Instancias

```csharp
// Obtener instancias disponibles
ApiInterface.Instance.GetInstances(
    instances => { foreach (var i in instances) Debug.Log(i.ID + " - " + i.Name); },
    err => Debug.LogError(err)
);

// Conectarse a una instancia por ID
ApiInterface.Instance.ConnectInstance("0",
    msg => Debug.Log("Conectado: " + msg),
    err => Debug.LogError(err)
);
```

### Lectura de Tags

```csharp
// Leer un tag genérico (devuelve string)
ApiInterface.Instance.GetTag("Motor", "Bool",
    value => Debug.Log("Motor: " + value),
    err => Debug.LogError(err)
);

// Leer un tag Bool (devuelve bool)
ApiInterface.Instance.GetTagBool("Motor",
    value => Debug.Log("Motor: " + value)
);

// Leer un tag entero (devuelve int)
ApiInterface.Instance.GetTagInt("Velocidad", "DInt (Int32)",
    value => Debug.Log("Velocidad: " + value)
);

// Obtener todos los tags con su valor en una sola petición
ApiInterface.Instance.GetTagsWithValues(
    tags => { foreach (var t in tags) Debug.Log(t.Name + ": " + t.Value); },
    err => Debug.LogError(err)
);
```

### Escritura de Tags

```csharp
// Escribir un tag genérico
ApiInterface.Instance.SetTag("Marcha", "Bool", "true",
    msg => Debug.Log(msg)
);

// Escribir un Bool
ApiInterface.Instance.SetTagBool("Marcha", true,
    msg => Debug.Log(msg)
);

// Escribir un entero
ApiInterface.Instance.SetTagInt("Velocidad", "DInt (Int32)", 150,
    msg => Debug.Log(msg)
);
```

### Polling Automático

Suscribe un tag para que se lea automáticamente cada `pollInterval` segundos  
(configurable en el Inspector de `ApiInterface`, por defecto `0.5s`):

```csharp
// Suscribir
ApiInterface.Instance.SubscribeTag("Motor", "Bool",
    value => Debug.Log("Motor: " + value)
);

// Cancelar suscripción
ApiInterface.Instance.UnsubscribeTag("Motor");
```

---

## Troubleshooting

- **HTTP 411 Length Required** — Las peticiones POST sin cuerpo deben incluir `-d ""` en cURL. `ApiInterface` ya lo gestiona internamente.
- **Peticiones bloqueando Unity** — Todas las llamadas de `ApiInterface` usan corrutinas internamente; nunca bloquean el hilo principal.
- **Sin input de teclado en el editor** — Haz clic dentro de la pestaña Game para que Unity capture el input.
- **La API no responde desde otra máquina** — Comprueba que el firewall tiene el puerto 9000 abierto y que `baseUrl` apunta a la IP correcta.