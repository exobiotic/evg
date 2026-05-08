using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace EvG.Models
{
    public class MapGeneratorInput
    {
        public string Name { get; set; } = "Generated Map";
        public int Width { get; set; } = 10;
        public int Height { get; set; } = 10;
        public int[] WalkableTiles { get; set; } = new[] { 11 };
        public int[] NonWalkableTiles { get; set; } = new[] { 1, 2, 3, 4, 5, 10, 12, 13, 14, 19, 20, 21 };
        public int NumUnits { get; set; } = 6;
        public string[] UnitTypes { get; set; } = new[] { "dwarf", "minotaur" };
    }

    public class MapGeneratorOutput
    {
        public string Name { get; set; }
        public string Tilemap { get; set; }
        public int[] OpenTiles { get; set; }
        public object[] Units { get; set; }
    }

    public class MapGenerator
    {
        private static readonly string MapBase =
            new FileInfo(System.Reflection.Assembly.GetAssembly(typeof(MapGenerator)).FullName)?.DirectoryName + "/wwwroot/assets/maps/";

        public static MapGeneratorOutput GenerateMap(MapGeneratorInput input)
        {
            if (input.Width < 5 || input.Height < 5)
            {
                throw new ArgumentException("Map dimensions must be at least 5x5");
            }

            if (input.NumUnits < 0)
            {
                throw new ArgumentException("Number of units must be non-negative");
            }

            if (input.WalkableTiles == null || input.WalkableTiles.Length == 0)
            {
                throw new ArgumentException("At least one walkable tile must be specified");
            }

            // Generate map data
            int[] mapData = GenerateMapData(input);

            // Create output
            var output = new MapGeneratorOutput
            {
                Name = input.Name,
                Tilemap = "basic/basic.json", // Using basic tileset
                OpenTiles = input.WalkableTiles.Distinct().ToArray(),
                Units = GenerateUnits(input, mapData, input.Width, input.Height)
            };

            return output;
        }

        public static void SaveMap(MapGeneratorOutput map, string filename)
        {
            if (!Directory.Exists(MapBase))
            {
                Directory.CreateDirectory(MapBase);
            }

            string filePath = Path.Combine(MapBase, filename + ".json");

            var json = JsonConvert.SerializeObject(map, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private static int[] GenerateMapData(MapGeneratorInput input)
        {
            int[] mapData = new int[input.Width * input.Height];
            Random random = new Random();

            // Generate random map with walkable and non-walkable tiles
            for (int i = 0; i < mapData.Length; i++)
            {
                // 70% chance of walkable tile, 30% non-walkable
                if (random.Next(100) < 70)
                {
                    mapData[i] = input.WalkableTiles[random.Next(input.WalkableTiles.Length)];
                }
                else
                {
                    mapData[i] = input.NonWalkableTiles.Length > 0
                        ? input.NonWalkableTiles[random.Next(input.NonWalkableTiles.Length)]
                        : input.WalkableTiles[0];
                }
            }

            return mapData;
        }

        private static object[] GenerateUnits(MapGeneratorInput input, int[] mapData, int width, int height)
        {
            if (input.NumUnits == 0)
            {
                return new object[0];
            }

            var units = new List<object>();
            Random random = new Random();
            HashSet<int> usedPositions = new HashSet<int>();
            HashSet<int> walkableTileSet = new HashSet<int>(input.WalkableTiles);

            int attempts = 0;
            int maxAttempts = input.NumUnits * 100;

            while (units.Count < input.NumUnits && attempts < maxAttempts)
            {
                attempts++;

                int x = random.Next(0, width);
                int y = random.Next(0, height);
                int positionKey = (x << 16) | y;

                // Check if position is valid (walkable and not occupied)
                if (!usedPositions.Contains(positionKey) && walkableTileSet.Contains(mapData[y * width + x]))
                {
                    usedPositions.Add(positionKey);
                    string unitType = input.UnitTypes[random.Next(input.UnitTypes.Length)];
                    units.Add(new { type = unitType });
                }
            }

            return units.ToArray();
        }

        public static dynamic LoadMap(string filename)
        {
            string filePath = Path.Combine(MapBase, filename + ".json");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Map file not found: {filePath}");
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject(json);
        }

        public static string[] GetAvailableMaps()
        {
            if (!Directory.Exists(MapBase))
            {
                return new string[0];
            }

            return Directory.GetFiles(MapBase, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToArray();
        }
    }
}
