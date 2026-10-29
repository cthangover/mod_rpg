# RPG World — camina por un mapa de tiles, habla, abre, lucha

RPG World convierte un juego en un RPG de mapas de tiles al estilo de los rol clásicos en 2D: avanzas por una cuadrícula con el teclado, hablas con la gente, abres cofres, cruzas puertas y luchas contra monstruos, y tras el combate vuelves al mismo punto. El mapa es una escena cualquiera del juego, así que el cuadro de diálogo, los escenarios, el guardado, la música y la hora del día funcionan en los mapas igual que en una novela visual.

Este mod da vida a la aldea, el bosque y el desierto de **RPG Plus**. Los mapas y sus habitantes vienen de mods de contenido como RPG Demo; este aporta las reglas.

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

El cofre de la casa del anciano son dos eventos en la misma casilla. El cerrado se muestra mientras la bandera de historia `rpg_house_chest` no esté activa; su escenario entrega tres monedas y activa la bandera. A partir de entonces se muestra en su lugar el cofre abierto.

![Antes: el héroe junto al cofre cerrado en la casa del anciano, al mediodía.](assets/chest-before.jpg)

![El texto del cofre: calcetines viejos, una nota de «No tocar» y tres monedas debajo.](assets/chest-text.jpg)

![Después: la misma casilla muestra ahora el cofre abierto, enmarcado por el marcador de interacción; la ayuda de abajo a la izquierda dice «E / F · Interact».](assets/chest-after.jpg)

```json
{ "Id": "chest", "X": 9, "Y": 7, "Tile": 14,
  "Scenario": "rpg_demo/events/house_chest",
  "Condition": "rpg_house_chest != 1" },
{ "Id": "chest_open", "X": 9, "Y": 7, "Tile": 15,
  "Scenario": "rpg_demo/events/chest_empty",
  "Condition": "rpg_house_chest == 1" }
```

```scenario
ptext "En el cofre: calcetines viejos y una nota que dice "No tocar". Demasiado tarde. Debajo de los calcetines: tres monedas." key=rpg_demo/chest/house
action item.add id=money/coin count=3
flag rpg_house_chest 1
end
```

## Para autores: mapas

Un mapa es un archivo JSON en la carpeta `maps/` de un mod, más una escena con el mismo nombre en `scenes/` para su fondo en los combates y su ambiente sonoro.

- **Tres capas dibujadas**, cada una una lista de filas con números de tile (`.` para vacío): `Back` — suelo, interiores, agua; `Main` — objetos a la altura de los personajes: muros, troncos, vallas; `Front` — se dibuja sobre los personajes: copas de los árboles, aleros, arcos.
- **Colisiones** — una capa de filas que sustituye la transitabilidad de los tiles donde hace falta: un muro invisible al borde de un acantilado o un hueco en un seto.
- **Tileset** — el atlas de tiles y las propiedades de cada uno (sólido o no, superficies, objetos).
- **Spawn** — dónde aparece quien llega nuevo; **Zoom**; el color **Ambient** opcional para interiores (si no, la hora del día ilumina el mapa) y **Atmosphere** (`woodland` añade hojas, luciérnagas y ondas en el agua).
- **Encounters** — combates aleatorios: de media uno cada `Steps` pasos contra uno de los `Groups`.

```json
"Encounters": { "Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"] }
```

## Para autores: eventos

Los eventos viven en `map_events/<id del mapa>/*.json` de cualquier mod. Un evento tiene un **aspecto** y un **efecto**, elegidos por separado:

- aspecto: un `Sprite` de personaje, un `Tile` suelto del atlas, el `Icon` de un objeto tirado en el suelo o nada (un desencadenante invisible);
- efecto: un `Teleport` a una casilla de este u otro mapa, o un `Scenario` seguido de `Commands` en línea opcionales — el lenguaje de escenarios completo, con `action rpg.battle`, banderas y misiones incluidas;
- `Trigger`: `Action` (E/F estando de cara) o `Touch` (al pisarlo);
- `Solid`: si bloquea el paso (por defecto, los eventos dibujados sí lo hacen);
- `Condition`: oculta el evento mientras no se cumpla la condición;
- `Pages`: aspectos y efectos alternativos, cada uno con su condición; se usa la última página cuya condición se cumpla;
- `Route`: movimiento autónomo como `{"Steps": ["left", "wait", "right"], "Every": 0.8}`, con `up`/`down`/`left`/`right`, `random`, `toward`/`away` (hacia el jugador o alejándose), `wait` y `face_…`;
- `Light`: un brillo alrededor del evento, `{"Radius": 3, "Color": "#ffd8a0"}`, visible al anochecer y de noche.

**Añade o cambia el mapa de otro mod.** Los archivos de eventos se combinan en orden de carga: un `Id` nuevo añade un evento al mapa, y el mismo `Id` de un mod que dependa del dueño lo sustituye. Así da RPG Plus nuevos diálogos al anciano de la demo.

## Acciones de escenario

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=rpg_wolf_dead
action rpg.teleport map=rpg_forest x=1 y=8 dir=right
```

- `rpg.battle` inicia un combate con el núcleo de combate del juego y vuelve a la misma casilla; al ganar, `win_flag` pasa a 1, así que el monstruo del mapa cuya condición comprueba esa bandera desaparece. Termina el escenario después de esta acción.
- `rpg.teleport` mueve al jugador a una casilla de este mapa o de otro.

## Ajustes del juego

- **Apariencia** (`rpg.player_charset`) — el sprite del héroe en los mapas, elegido entre los charsets del juego.
- **Proyección del mapa** (`tile.renderer`) — vista superior o isométrica; ver Tile Rendering.

## Si algo falla

1. **Un personaje o un cofre no reacciona.** Colócate de frente y pulsa E o F; un evento `Touch` solo responde al pisarlo.
2. **Un monstruo o un cofre vuelve a aparecer.** Su `Condition` debe comprobar la bandera que activa su propio escenario o `win_flag`.
3. **No puedes pasar por un sitio que parece libre.** Esa casilla la bloquea un tile sólido o la capa de colisiones del mapa.
4. **No hay fondo de combate.** La escena del mapa en `scenes/<map>.json` necesita un `defaultBackground`.
