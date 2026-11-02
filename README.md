# devaZtation (VR_T1)

Shooter de supervivencia contra zombies en realidad virtual para Meta Quest. Empiezas en un tutorial, cruzas un portal a una ciudad tomada por zombies y aguantas oleadas cada vez más grandes hasta llegar al helicóptero de escape.

## Requisitos

- **Unity 6000.2.6f2** con el módulo **Android Build Support**.
- Un Meta Quest (2, 3 o Pro) con el modo desarrollador activado, o Quest Link para probar desde el editor.
- Paquetes principales: XR Interaction Toolkit 3.2, OpenXR 1.15 (Meta Quest Support), AI Navigation 2.0.

## Escenas

| Escena | Qué es |
|---|---|
| `Assets/Scenes/Prototipable.unity` | Tutorial (índice 0). El portal lleva al nivel. |
| `Assets/Scenes/level_00.unity` | Nivel de la ciudad (índice 1). |
| `Assets/Scenes/LVL_design/` | Versiones de la rama `LVL_design` guardadas aparte para comparar; no están en el build. |

## Cómo se juega

- **Moverse:** stick izquierdo. **Girar:** stick derecho.
- **Agarrar un arma:** botón grip. **Disparar:** gatillo (la Uzi y la M4 disparan en automático mientras lo mantienes).
- **Recargar:** apunta el arma al suelo un momento. La munición sale de la reserva compartida; las cajas de munición la rellenan. El número sobre el arma muestra las balas que quedan en el cargador.
- **Tiros a la cabeza:** hacen el triple de daño.
- **Cuchillos:** golpea con ellos o lánzalos; si van rápido se clavan en zombies y paredes.
- **Martillo de Thor:** golpea, o lánzalo para una onda expansiva al impactar. Con **B / Y** vuelve en curva a la última mano que lo sostuvo y golpea a lo que encuentre en el camino.
- **Barriles rojos:** explotan con disparos, golpes fuertes o el martillo, y hacen explotar a los barriles cercanos. También te hacen daño si estás cerca.
- **Moto:** agarra un puño del manubrio para subirte. Con las dos manos en el manubrio giras moviéndolo como uno real; con una sola, usa el stick izquierdo. **Gatillo derecho** acelera, **gatillo izquierdo** frena y da marcha atrás, **A / X** te baja (hay que ir despacio). Atropellar zombies les hace daño según la velocidad. Solo se inclina la moto, no tu vista, para marear menos; la viñeta de comodidad se activa con la velocidad.
- **Gancho:** apunta (aparece una mira verde), mantén el gatillo para engancharte y balancearte, y usa el **stick de esa mano** hacia arriba o abajo para recoger o soltar cuerda. Se engancha a cualquier superficie del escenario.
- **Ganas** si llegas al helicóptero. **Pierdes** si te quedas sin vida o sin tiempo. En la pantalla final, aprieta el gatillo para continuar.

### Opciones de comodidad (botón menú del control izquierdo)

| Acción | Efecto |
|---|---|
| Toque corto | Giro por pasos ↔ giro continuo |
| Mantener 1 s | Modo sentado ↔ de pie |
| Mantener 3 s | Viñeta al moverse sí ↔ no |

Las opciones se guardan entre partidas. En el editor, la tecla **M** hace lo mismo.

## Estructura del código

```
Assets/Scripts/
  Core/      GameManager (estado de la partida, vida, munición, tiempo), VRHud (avisos, viñeta, pantalla final)
  Player/    PlayerHealth, ComfortSettings
  Weapons/   Weapon + WeaponData (armas de fuego), MeleeWeapon (golpes y lanzables), ThorHammer,
             ExplosiveBarrel, GrapplingGun, Hitbox, Magazine; Bspawn/Buspawn/Bullet (sistema antiguo)
  Enemies/   ZombieController, EnemySpawnPoint, WaveDirector (oleadas con pool)
  Vehicles/  Motorcycle
  Level/     Port (portal del tutorial), PortHelicopter (victoria), Waypoint
Assets/Data/Weapons/   Ajustes de cada arma (daño, cadencia, cargador, sonidos)
Assets/Editor/         VRT1Migration y QuestOptimizer (menú VR_T1)
```

- **Ajustar un arma:** edita su asset en `Assets/Data/Weapons/`.
- **Ajustar las oleadas:** el `WaveDirector` se crea solo al jugar. Para cambiar sus valores, agrega un objeto con `WaveDirector` a `level_00`; los zombies salen de los `EnemySpawnPoint` de la escena.
- **Menú VR_T1 en el editor:**
  - *Migrar armas y zombies:* convierte prefabs con el sistema antiguo de armas y agrega la hitbox de cabeza a los zombies. Se puede repetir sin problema.
  - *Optimizar para Quest:* ajustes de Android/OpenXR, calidad, límites de texturas, compresión de mallas y objetos estáticos.
  - *Calcular occlusion culling (level_00):* también marca los objetos estáticos.
  - *Crear martillo, cuchillos y barriles:* también crea la moto. Genera los prefabs en `Assets/Prefabs/Gameplay/` y los coloca en las escenas (objeto `Gameplay (generado)`). El martillo y el barril son formas simples hasta que haya modelos propios; para cambiarlos, reemplaza las piezas visuales del prefab.

## Compilar para Quest

1. File → Build Profiles → Android → *Switch Platform*.
2. Conecta el Quest por USB y elige *Build And Run*.

`user.keystore` ya no está en el repositorio. Para firmar un build de publicación, cada persona usa su propia clave en Player Settings → Publishing Settings.

## Créditos

Proyecto universitario de 2022 de bruno/brunopla, eli-173 (Elias), WaruGameDev, KaitoOwO y waldolagos. Migrado a Unity 6 en 2026.
