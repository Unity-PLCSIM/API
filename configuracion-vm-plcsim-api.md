# Configuración: acceso desde host a API sobre PLCSIM Advanced en VM

Documento de referencia con la configuración que hace funcionar el montaje.
Última verificación: 2026-07-28.

---

## 1. Topología

```
PC-A (tu host)                    PC-B (anfitrión de la VM)
192.168.1.208/24                         │
      │                                  │
      └──────── LAN 192.168.1.0/24 ──────┤
                gateway 192.168.1.1      │
                                         │
                              VM (VMware Workstation 17.6.3)
                              adaptador en modo BRIDGED
                              192.168.1.50/24
                                   │
                                   ├─ API self-hosted  :9000
                                   │        │ (DLL Siemens, softbus)
                                   └─ PLCSIM Advanced V7.0
                                      instancia 192.168.100.1
```

**Idea clave:** el host solo habla HTTP contra el puerto 9000. Todo el diálogo con
el PLC ocurre dentro de la VM. El host nunca necesita alcanzar la red del PLC.

---

## 2. Red de la VM

### Modo de red en VMware

**Bridged**, obligatoriamente. En *VM → Settings → Network Adapter*:

- Marcar **Bridged: Connected directly to the physical network**
- En **Configure Adapters**, seleccionar **solo la tarjeta Ethernet física** del PC-B.
  Dejarlo en *Automatic* puede hacer que VMware puentee contra un adaptador de VPN.

> **NAT no sirve.** En NAT la VM queda detrás del NAT del PC-B: solo ese PC la
> alcanza. Cualquier otro equipo de la red no. Funcionaría únicamente si la API
> se consumiera desde el propio PC-B.

### Direccionamiento dentro de la VM

Adaptador `Ethernet0` (la NIC de VMware), **IP fija**:

| Parámetro | Valor |
|---|---|
| IP | `192.168.1.50` |
| Máscara | `255.255.255.0` (**/24**) |
| Gateway | `192.168.1.1` |
| DNS | `192.168.1.1` |

```powershell
Remove-NetIPAddress -InterfaceAlias "Ethernet0" -Confirm:$false
New-NetIPAddress -InterfaceAlias "Ethernet0" -IPAddress 192.168.1.50 -PrefixLength 24 -DefaultGateway 192.168.1.1
Set-DnsClientServerAddress -InterfaceAlias "Ethernet0" -ServerAddresses 192.168.1.1
```

> **La máscara /24 es crítica.** Tiene que coincidir con la del host. Con una /23
> la VM considera al host vecino directo y le responde por el switch, mientras que
> el host la cree en otra red y enruta por el router: el tráfico va por un camino
> y vuelve por otro. El síntoma es engañoso — el ping funciona y el TCP aparenta
> abrir — pero las peticiones HTTP nunca se completan.

IP fija a propósito: si el DHCP de la red reparte /23, volver a DHCP reintroduce
el problema.

### Perfil de red

`Ethernet0` debe estar en **Private**:

```powershell
Set-NetConnectionProfile -InterfaceAlias "Ethernet0" -NetworkCategory Private
```

### Firewall

```powershell
New-NetFirewallRule -DisplayName "API PLC 9000" -Direction Inbound -Protocol TCP -LocalPort 9000 -Action Allow
```

---

## 3. PLCSIM Advanced V7.0

En el **Control Panel**:

| Campo | Valor |
|---|---|
| Online Access | **TCP/IP Single Adapter** |
| TCP/IP communication with | `<Local>` |
| Instance name | `PLC_SEATS_4` |
| IP address [X1] | `192.168.100.1` |
| Subnet mask | `255.255.255.0` |
| Default gateway | *(vacío)* |
| PLC family | **S7-1500** (debe coincidir con la CPU del proyecto) |

`<Local>` publica la instancia solo dentro de la VM. Es lo correcto aquí: la API
corre en la misma máquina y así el puerto S7 no queda expuesto a la red.

### El adaptador virtual de PLCSIM: no tocar

En la VM aparece un adaptador con alias `Ethernet` y descripción
*Siemens PLCSIM Virtual Ethernet Adapter*, con una IP APIPA `169.254.x.x` y perfil
*Public*. **Es normal y debe quedarse así.** La API usa la DLL de Siemens
(Runtime API, comunicación por softbus), no S7 por red, así que ese adaptador no
necesita una IP válida.

> Cuidado con los nombres: el alias `Ethernet` es el adaptador de PLCSIM y
> `Ethernet0` es la tarjeta de VMware. Es fácil confundirlos. Para desambiguar,
> usar `-InterfaceIndex` en vez de `-InterfaceAlias`.

### Lo que NO aplica a este montaje

Al usar la DLL de Siemens y no un cliente S7 (snap7, S7netplus…), estas cosas son
irrelevantes:

- Puerto **102** / protocolo S7
- *Permit access with PUT/GET communication from remote partner*
- DBs con *Optimized block access* desmarcado
- Rack/slot
- **NetToPLCSim** (solo hace falta con PLCSIM clásico, no con Advanced)

---

## 4. API self-hosted

- Puerto **9000**, sobre **http.sys** (en `netstat` el PID es **4**, el proceso
  `System`; es lo esperado con `HttpListener`).
- Prefijo con comodín fuerte, **no** `localhost`:

```csharp
listener.Prefixes.Add("http://+:9000/");
```

- Reserva de URL necesaria para el comodín (PowerShell admin, una sola vez):

```powershell
netsh http add urlacl url=http://+:9000/ user=Todos
```

  (en Windows en inglés: `user=Everyone`)

- URL base desde el host: **`http://192.168.1.50:9000/api/plc/<método>`**

  El enrutado espera el segmento de acción. Un
  `GET /api/plc` sin más devuelve 404 con el mensaje
  *"No action was found on the controller 'Plc'"* — eso indica que la red
  funciona y que solo falta el nombre del método.

---

## 5. Orden de arranque

La instancia de PLCSIM **no** sobrevive a un reinicio. Tras arrancar la VM:

1. Abrir el **Control Panel de PLCSIM Advanced** y arrancar la instancia (`Start`).
2. Cargar el proyecto de TIA en la instancia si no está ya, y ponerla en **RUN**.
3. Arrancar la **API**.

Los dos últimos pasos, **en la misma sesión interactiva de Windows**. Si la API se
ejecuta como servicio arranca en la sesión 0 y no verá la instancia de PLCSIM,
aunque sus endpoints respondan.

---

## 6. Verificación

**En la VM:**

```powershell
netstat -ano | findstr :9000
```
Debe mostrar `0.0.0.0:9000 ... LISTENING 4`.

**Desde el host:**

```powershell
Test-NetConnection -ComputerName 192.168.1.50 -Port 9000
```
Lo que importa es `TcpTestSucceeded : True`.

```powershell
Invoke-WebRequest -Uri "http://192.168.1.50:9000/api/plc" -UseBasicParsing
```
Un 404 con cuerpo JSON de Web API ya confirma que la cadena de red está completa.

---

## 7. Diagnóstico de problemas

| Síntoma | Causa probable |
|---|---|
| Ping OK, TCP "abierto", HTTP expira | Máscaras distintas entre host y VM → enrutado asimétrico |
| `netstat :9000` vacío | La API no está corriendo |
| `netstat` muestra `127.0.0.1:9000` | Prefijo `localhost`; cambiar a `http://+:9000/` |
| TCP cerrado desde el host, OK en local | Firewall de la VM, o perfil de red en *Public* |
| *Access Denied* al arrancar la API | Falta la reserva `urlacl` |
| La API responde pero falla contra el PLC | Instancia parada, en STOP, o API en sesión distinta |
| Adaptador en `169.254.x.x` | DHCP sin servidor: poner IP fija (salvo el adaptador de PLCSIM, donde es normal) |

### Notas sueltas

- **No cambiar la configuración de red con TIA Portal abierto.** Si el adaptador
  al que está enlazada la interfaz PG/PC desaparece, TIA Portal se cierra con
  error y se pierde lo no guardado.
- **No usar rangos de IP públicos** para redes locales o simuladas. Un
  `121.31.57.x`, por ejemplo, es espacio público real (APNIC): el tráfico hacia
  esas direcciones se enruta hacia Internet. Usar solo `10.0.0.0/8`,
  `172.16.0.0/12` o `192.168.0.0/16`.
- En el host, `Get-NetAdapter` filtrado por `InterfaceAlias` da falsos negativos
  con estos adaptadores. Filtrar por `InterfaceDescription`.
