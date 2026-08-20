# Integración de PLCSim Advanced con Unity mediante API REST

Puente de comunicación bidireccional entre PLCSim Advanced y Unity a través de una API REST intermediaria en C# (.NET Framework).

---

## Lanzar la API
Ejecuta el compilado de la API **como Administrador** en la máquina donde corre PLCSim Advanced.  
Si Unity y la API están en máquinas distintas, abre el puerto en el firewall:
```bash
netsh advfirewall firewall add rule name="API PLCSim Port 9000" dir=in action=allow protocol=TCP localport=9000
```
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

## Troubleshooting

- **HTTP 411 Length Required** — Las peticiones POST sin cuerpo deben incluir `-d ""` en cURL. `ApiInterface` ya lo gestiona internamente.
- **La API no responde desde otra máquina** — Comprueba que el firewall tiene el puerto 9000 abierto y que `baseUrl` apunta a la IP correcta.
