Creating a new Level
====================

A scene template exists to create a new level. Just create a new scene using that template.

This scene contains :
- The Game UI
- The Camera
- LevelData that contains all the level information **change this settings for your level**: 
  - How many moves the player have
  - What are the goals to win the levels.
- A Grid that have 2 children tilemaps :
  - **Background** : this is just the visual background of the level.
  - **Logic** : this contains all the logics tiles. Those tiles won't be kept at runtime, and are just here to generate data for the games

In practice, you can have as many tilemap as you wants and can sprite your tiles on multiple tilemap as in the end, the board data is stored in a 
custom array in memory and those tiles are just used for authoring the level, but keeping those separated that way make it easier to work.