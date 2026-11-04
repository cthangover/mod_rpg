# RPG World — camina por un mapa de tiles, habla, abre, lucha

RPG World convierte un juego en un RPG de mapas de tiles al estilo de los rol clásicos en 2D: avanzas por una cuadrícula con el teclado, hablas con la gente, abres cofres, cruzas puertas y luchas contra monstruos, y tras el combate vuelves al mismo punto. El mapa es una escena cualquiera del juego, así que el cuadro de diálogo, los escenarios, el guardado, la música y la hora del día funcionan en los mapas igual que en una novela visual.

Este mod da vida a la aldea, el bosque y el desierto de **RPG Plus**. Los mapas y sus habitantes vienen de mods de contenido como `rpg_plus_game`; este aporta las reglas.

![Explorando la aldea de RPG Plus: el nombre del mapa arriba a la izquierda, la barra de herramientas arriba a la derecha y la ayuda de controles abajo a la izquierda.](assets/village.jpg)

## Cómo jugar en un mapa

- **Camina** con WASD o las flechas; mantén pulsado **Shift** para correr.
- **Actúa** con **E** o **F**: habla con quien tengas delante, abre el cofre, lee el cartel.
- **Pisa** una puerta o el borde del mapa para pasar al siguiente.
- En algunos sitios, como el bosque, los **combates aleatorios** pueden empezar mientras caminas.
- Tras un combate vuelves a la misma casilla del mismo mapa.
- El juego guarda tu posición; al cargar reapareces exactamente ahí.

La casilla con la que puedes interactuar se enmarca, y la ayuda de abajo a la izquierda cambia a **E / F · Interact**.

## Ejemplo: un cofre que sigue abierto

El cofre de la casa del anciano es una sola colocación del objeto «cofre», con dos estados. Mientras no esté puesto el indicador de historia `rpg_house_chest`, se ve el cofre cerrado; su escenario da tres monedas y pone el indicador. Desde entonces se ve el estado abierto.

![Antes: el héroe junto al cofre cerrado en la casa del anciano, al mediodía.](assets/chest-before.jpg)

![El texto del cofre: calcetines viejos, una nota de «No tocar» y tres monedas debajo.](assets/chest-text.jpg)

![Después: la misma casilla muestra ahora el cofre abierto, enmarcado por el marcador de interacción; la ayuda de abajo a la izquierda dice «E / F · Interact».](assets/chest-after.jpg)

```json
{"Id": "rpg_elder_house/chest", "Object": "village_pack/chest", "X": 9, "Y": 7,
 "Interact": {"Scenario": "rpg_plus/events/house_chest", "Commands": null},
 "States$add": [{"Id": "open", "When": "rpg_house_chest == 1",
                 "Interact": {"Scenario": "rpg_plus/events/chest_empty", "Commands": null}}]}
```

```scenario
ptext "En el cofre: calcetines viejos y una nota que dice "No tocar". Demasiado tarde. Debajo de los calcetines: tres monedas." key=rpg_plus/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## Para autores: mapas

Un mapa es un archivo JSON en la carpeta `maps/` de un mod. Solo contiene el suelo: lo que hay sobre él (árboles, casas, barriles, personas) se coloca con colocaciones. La escena con el mismo nombre en `scenes/` da el fondo de los combates y el sonido ambiente del mapa.

- **Legend**: una letra por cada tipo de superficie o valla: `"g": "village_pack/ground/grass"`. El punto `.` es una celda vacía.
- **Ground** y **Structure**: filas de letras por celda: superficies (hierba, agua, suelo) y vallas o muros.
- **Collision**: filas de `#` (bloqueado), `o` (paso abierto a la fuerza, por ejemplo un hueco en un muro) y `.` (lo deciden los objetos).
- **Spawn**, **Zoom**, **Ambient** (color de luz fijo para interiores), **Atmosphere** (`woodland`, `winter_snow`, `desert_dust`, `rain`) y **Encounters** (`Steps` y `Groups`):

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## Para autores: objetos y colocaciones

Los objetos se definen en `objects/*.json` (`rpg_object`). Una colocación pone un objeto en un mapa: un archivo `placements/<mapa>/*.json` (`rpg_placement`) con un `Id` del tipo `<mapa>/<nombre>`, `Object`, `X`, `Y` y los campos que cambian solo esa instancia.

- **Form**: `surface` (superficie), `segment` (un trozo de valla que se une a sus vecinos), `sprite` (una imagen sobre un área de celdas: árboles, casas, barriles, puertas) o `actor` (un personaje con su hoja de caminar).
- **Solid**: bloquea el paso. Un sprite bloquea por defecto; un disparador necesita `"Solid": false`.
- **Interact** — `Trigger`: `Use` (E/F mirando al objeto) o `Step` (al entrar). Acepta `Scenario`, `Commands` o ambos: las órdenes se ejecutan después del escenario, incluso tras su `end`. `Teleport` (`{Map, X, Y, Dir}`) actúa solo y omite el diálogo.
- **States**: `When` (una condición) y qué cambia: `Hidden`, otra imagen, `Interact`, `Route`. Se aplica el último estado cuyo `When` se cumpla. En una colocación, `self.nombre` es su propio indicador `<Id>:nombre`.
- **Route**, **Light** y **Lights**: como antes (`Steps`, `Every`; un resplandor con `Radius`, `Color` y la posición `At` en celdas desde el objeto).
- **Extends** y **Variants**: heredar de otro objeto y mantener versiones (por ejemplo, una de invierno).
- **Añadir al mapa de otro mod**: pon una colocación con tu propio `Id`. Para reemplazar una ajena, usa su mismo `Id` con `depends` hacia el dueño; para borrarla, un parche con `"$delete": true`.
- **Aspecto del héroe**: el `Charset` del líder del grupo (por ejemplo `village/hero`).

## Acciones de escenario

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` inicia un combate con el núcleo de combate del juego y vuelve a la misma casilla; al ganar, `win_flag` pasa a 1, así que el monstruo del mapa cuya condición comprueba esa bandera desaparece. Termina el escenario después de esta acción.
- `rpg.teleport` mueve al jugador a una casilla de este mapa o de otro.

## Ajustes del juego

- **Proyección del mapa** (`tile.renderer`): vista desde arriba o isométrica; ver Tile Rendering.

## Si algo falla

1. **Un personaje o un cofre no reacciona.** Ponte frente a él y pulsa E o F. Un objeto con `Step` reacciona al pisarlo.
2. **Un monstruo o un cofre vuelve.** El estado que lo oculta o lo cambia debe comprobar el indicador que pone su escenario o `win_flag`.
3. **No puedes pasar por donde parece abierto.** Un objeto sólido (`Solid`) o una `#` en la colisión del mapa bloquea la celda.
4. **No hay fondo de combate.** La escena del mapa en `scenes/<map>.json` necesita un `defaultBackground`.

## Eventos del mapa y combates opcionales

Las zonas invisibles se guardan en `Events` del mapa: `Id` local, `X`, `Y`, `Width`, `Height`, `Trigger`, `When`, `Solid` y `Scenario` con `Commands` o un `Teleport` independiente. No necesitan objeto ni colocación. `self.done` pertenece a `<map>/<event>:done`; conserve el ID. En el editor elija Event, dibuje el rectángulo, escriba las órdenes, Apply event y Save.

El mundo no depende de combates. `rpg.battle` y los encuentros aleatorios requieren también `turn_battle_base`, mediante `rpg__battles`. El grupo sigue al líder; `npc_barks_base` añade conversaciones de fondo simultáneas.
