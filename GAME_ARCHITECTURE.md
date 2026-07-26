# Silent Decay - Documentación de Arquitectura y Lógica del Juego

Este documento sirve como guía de contexto permanente sobre la estructura, sistemas principales y lógica de programación del proyecto **Silent Decay (Horror Game)** desarrollado en Unity URP.

---

## 1. Núcleo del Juego y Persistencia (`Core`)

* **`GameManager.cs`** (`Assets/StarterAssets/FirstPersonController/Scripts/GameManager.cs`)
  * **Patrón Singleton:** Instancia persistente mediante `DontDestroyOnLoad`.
  * **Sistema de Vidas:** Maneja las vidas del jugador (`maxVidas = 3`).
  * **Punto de Respawn:** Guarda la posición y rotación (`RegistrarSpawnJugador`) donde se genera el jugador al inicio de cada mapa procedural para reaparecer allí al morir si le quedan vidas.
  * **Control del Estado:** Administra transiciones entre menú, escenas de carga, juego activo y *Game Over*, asegurando que `Time.timeScale` se restablezca correctamente.

---

## 2. Generación Procedural de Nivel (`Procedural Generation Architecture`)

El juego incluye dos sistemas independientes de generación procedural de mapa:

### A. Hospital Modular (`ModularHospital System`)
Ubicación: `Assets/Dnk_Dev/HospitalHorrorPack/switch/ModularHospital/`
* **`ModularHospitalGenerator.cs`:** Ensambla habitaciones, pasillos y esquinas a través de conectores modulares.
* **`HospitalModule.cs` & `ModuleConnector.cs`:** Definen la geometría de los módulos y los puntos/nodos de unión orientados.
* **`ModuleDatabase.cs`:** Catálogo que clasifica prefabs en habitaciones, pasillos e intersecciones.
* **`ModuleValidator.cs`:** Sistema de comprobación anticolisión basado en límites de caja (`Bounds`) para evitar solapamientos.
* **Mapeo de Puzzles e Ítems:** Spawnea aleatoriamente la caja de fusibles (`fuseBoxPrefab`), fusibles, subgeneradores A y B, notas con pistas (`notePrefab`), baterías y el teclado de código (`correctKeypadCode`), culminando con el módulo del ascensor de escape (`elevatorPrefab`).

### B. Túneles y Alcantarillas (`Tunnels System`)
Ubicación: `Assets/TunnelsMap/TunnelsGenerator.cs`
* **`TunnelsGenerator.cs`:** Genera un laberinto en rejilla 2D dinámico con pasarelas de lámina oxidada (*catwalks*), arcos de tuberías y paredes de concreto.
* **Iluminación Dinámica:** Distribuye focos de luz regulando zonas seguras y pasillos a oscuras (`maxDarkSpacing`).
* **Secuencia de Evacuación:** Implementa la máquina de estados de escape (`Idle` -> `Draining` -> `Ready`) activada mediante minijuego de consola/palanca con temporizador de drenaje (45s).
* **NavMesh Dinámico:** Reconstruye en tiempo real la malla de navegación (`NavMeshSurface`) para la IA.

---

## 3. Mecánicas del Jugador y Supervivencia (`Player Systems`)

* **`PlayerSanity.cs`:**
  * Controla la cordura del jugador (0 a 100).
  * La cordura disminuye progresivamente al estar a oscuras (`darkDrainRate`) o al mirar directamente al enemigo (`monsterDrainRate`).
  * Se recupera bajo la luz (`lightRestoreRate`).
  * Al bajar la cordura, genera un Vignette procedural oscuro y activa susurros 2D envolventes.
* **`HideUnderBed.cs`:**
  * Permite interactuar con camas o estructuras para ocultarse.
  * Desactiva temporalmente el `CharacterController` y el renderizado del jugador para evadir el campo de visión del enemigo.
* **`PlayerHealth.cs` & `FlashlightController.cs`:**
  * Controlan los puntos de vida, daño por ataques y la energía/batería de la linterna.

---

## 4. Inteligencia Artificial de las Criaturas (`Enemy AI System`)

* **`PhenomenonAIController.cs`** (`Assets/ThePhenomenon/Scripts/PhenomenonAIController.cs`)
  * Funciona con una Máquina de Estados Finitos (**FSM**):
    $$\text{Patrol} \longleftrightarrow \text{Alert} \longrightarrow \text{Investigate} \longleftrightarrow \text{Chase} \longrightarrow \text{Attack}$$
    $$\searrow \text{ObservingLight} \swarrow$$
  * **Sensibilidad a la Luz (`ObservingLight`):** Si el jugador entra en una zona iluminada, la criatura detiene su persecución y lo observa pacientemente desde el borde de la sombra antes de retirarse.
  * **Sistema de Detección:** Utiliza raycasting/FieldOfView y escucha ruidos generados por carreras o interacciones.
  * **Audio 3D Dinámico:** Controla efectos de arrastre de garras (`dragFingersSound`) y silbidos lejanos para aumentar la tensión.

---

## 5. Estructura de Control de Versiones (`Git Workflow`)

* **`main`:** Rama estable y de producción con código probado.
* **`dev`:** Rama activa de desarrollo para nuevas características y experimentos.
* **`.gitignore`:** Filtra directorios pesados/temporales de Unity (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, builds binarias).
