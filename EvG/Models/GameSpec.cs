using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace EvG.Models
{
    public class GameSpec
    {
        private static readonly string MapBase =
            new FileInfo(System.Reflection.Assembly.GetAssembly(typeof(GameSpec)).FullName).DirectoryName + "/wwwroot/assets/maps/";
        private static readonly RandomNumberGenerator random = RandomNumberGenerator.Create()!;
        public dynamic? Map { get; private set; }
        public string? Name { get; private set; }
        public string? Tilemap { get; private set; }
        public bool[][]? FloorMap { get; private set; }
        public Unit[]? Units { get; private set; }
        public Player[]? Players { get; set; }
        public bool Active { get; set; }
        private HashSet<int> openTileIds = new HashSet<int>();

        public GameSpec() : this(GetRandomMap(), new GameConfig()) { }

        public GameSpec(GameConfig gameConfig) : this(GetRandomMap(), gameConfig) { }

        public GameSpec(string map) : this(map, new GameConfig()) { }

        public GameSpec(string map, GameConfig gameConfig)
        {
            using (StreamReader reader = new StreamReader(MapBase + map + ".json"))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var serializer = new JsonSerializer();
                dynamic game = serializer.Deserialize(jsonReader);
                Name = game.name;
                Tilemap = $"/assets/maps/{game.tilemap}";
                CreateMap(serializer, game);
                CreateUnits(game, gameConfig);
            }
        }

        private static string GetRandomMap()
        {
            var maps = Directory.GetFiles(MapBase, "*.json").Select(f => f.Substring(0, f.LastIndexOf('.'))).ToArray();
            if (maps.Length == 0)
            {
                return null;
            }

            var nums = new byte[2];
            random.GetBytes(nums);
            var map = maps[(nums[0] << 8 | nums[1]) % maps.Length];
            map = map.Split(new []{ '\\', '/' }).Last();
            return map;
        }

        private void CreateMap(JsonSerializer serializer, dynamic game)
        {
            using (StreamReader mapReader = new StreamReader(MapBase + game.tilemap))
            using (var jsonMapReader = new JsonTextReader(mapReader))
            {
                var imageBase = Tilemap.Substring(0, Tilemap.LastIndexOf('/') + 1);
                Map = serializer.Deserialize(jsonMapReader);
                for (var i = 0; i < Map.tilesets.Count; i++)
                {
                    Map.tilesets[i].image = imageBase + Map.tilesets[i].image;
                }
                BuildFloorMap(game);
            }
        }

        private void CreateUnits(dynamic game, GameConfig gameConfig)
        {
            var random = new Random();
            int width = Map.width;
            int height = Map.height;
            HashSet<int> unitPositions = new HashSet<int>();
            Units = new Unit[game.units.Count];
            for (var i = 0; i < game.units.Count; i++)
            {
                var unit = game.units[i];
                Units[i] = new Unit()
                {
                    Type = game.units[i].type,
                    Health = Math.Max(1, gameConfig.UnitHealth),
                    Power = Math.Max(1, gameConfig.AttackDamage)
                };

                int x = random.Next(0, width);
                int y = random.Next(0, height);
                while (!FloorMap[x][y] || unitPositions.Contains((x << 16) | y))
                {
                    x = random.Next(0, width);
                    y = random.Next(0, height);
                }
                unitPositions.Add((x << 16) | y);
                Units[i].X = x;
                Units[i].Y = y;
            }
        }

        private void BuildFloorMap(dynamic game)
        {
            var floorData = Map.layers[0].data;
            int mapWidth = Map.width;
            int mapHeight = Map.height;
            FloorMap = new bool[mapWidth][];
            for (var i = 0; i < mapWidth; i++)
            {
                FloorMap[i] = new bool[mapHeight];
            }
            HashSet<int> openTiles = new HashSet<int>();
            foreach (int openTile in game.openTiles)
            {
                openTiles.Add(openTile);
            }
            openTileIds = openTiles;

            for (var i = 0; i < floorData.Count; i++)
            {
                int tileId = (int)floorData[i];
                FloorMap[i % mapWidth][i / mapWidth] = openTileIds.Contains(tileId);
            }
        }

        public void ResizeMap(int newWidth, int newHeight)
        {
            if (FloorMap == null || Map?.layers == null || Map.layers.Count == 0 || newWidth <= 0 || newHeight <= 0)
                return;

            int oldWidth = Map.width;
            int oldHeight = Map.height;

            ResizeMapLayerBySampling(oldWidth, oldHeight, newWidth, newHeight);
            
            // Update Map object dimensions
            Map.width = newWidth;
            Map.height = newHeight;

            RebuildFloorMapFromLayerData(newWidth, newHeight);
            
            // Regenerate unit positions to be valid in the resized map
            RegenerateUnitPositions();
        }

        private void ResizeMapLayerBySampling(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            if (Map?.layers == null || Map.layers.Count == 0)
                return;

            var layer = Map.layers[0];
            var oldData = ((JArray)layer.data).Select(t => (int)t).ToArray();
            int preferredOpenTileId = GetPreferredOpenTileId(oldData);
            int preferredWallTileId = GetPreferredWallTileId(oldData);
            int topEdgeWallTileId = GetPreferredEdgeWallTileId(oldData, oldWidth, oldHeight, "top", preferredWallTileId);
            int bottomEdgeWallTileId = GetPreferredEdgeWallTileId(oldData, oldWidth, oldHeight, "bottom", preferredWallTileId);
            int leftEdgeWallTileId = GetPreferredEdgeWallTileId(oldData, oldWidth, oldHeight, "left", preferredWallTileId);
            int rightEdgeWallTileId = GetPreferredEdgeWallTileId(oldData, oldWidth, oldHeight, "right", preferredWallTileId);
            int topLeftCornerTileId = EnsureWallTile(oldData[0], preferredWallTileId);
            int topRightCornerTileId = EnsureWallTile(oldData[oldWidth - 1], preferredWallTileId);
            int bottomLeftCornerTileId = EnsureWallTile(oldData[(oldHeight - 1) * oldWidth], preferredWallTileId);
            int bottomRightCornerTileId = EnsureWallTile(oldData[(oldHeight - 1) * oldWidth + (oldWidth - 1)], preferredWallTileId);
            var newData = new int[newWidth * newHeight];
            var sampledOldX = new int[newWidth];
            var sampledOldY = new int[newHeight];

            for (int x = 0; x < newWidth; x++)
            {
                sampledOldX[x] = MapIndexCentered(x, newWidth, oldWidth);
            }

            for (int y = 0; y < newHeight; y++)
            {
                sampledOldY[y] = MapIndexCentered(y, newHeight, oldHeight);
            }

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int oldX = sampledOldX[x];
                    int oldY = sampledOldY[y];

                    int oldIndex = oldY * oldWidth + oldX;
                    int newIndex = y * newWidth + x;
                    newData[newIndex] = oldData[oldIndex];
                }
            }

            // Thin walls that became duplicated by upscaling while preserving odd shapes.
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    if (x == 0 || y == 0 || x == newWidth - 1 || y == newHeight - 1)
                    {
                        continue;
                    }

                    int index = y * newWidth + x;
                    int tileId = newData[index];
                    if (openTileIds.Contains(tileId))
                    {
                        continue;
                    }

                    int oldX = sampledOldX[x];
                    int oldY = sampledOldY[y];
                    bool openLeft = IsOpenInSource(oldData, oldWidth, oldHeight, oldX - 1, oldY);
                    bool openRight = IsOpenInSource(oldData, oldWidth, oldHeight, oldX + 1, oldY);
                    bool openUp = IsOpenInSource(oldData, oldWidth, oldHeight, oldX, oldY - 1);
                    bool openDown = IsOpenInSource(oldData, oldWidth, oldHeight, oldX, oldY + 1);

                    // Only thin true one-tile-thick lines; keep corners and shape joints intact.
                    bool thinVertical = (openLeft && openRight) ||
                        (oldX == 0 && openRight) ||
                        (oldX == oldWidth - 1 && openLeft);
                    bool thinHorizontal = (openUp && openDown) ||
                        (oldY == 0 && openDown) ||
                        (oldY == oldHeight - 1 && openUp);

                    bool duplicateX = x > 0 && sampledOldX[x - 1] == oldX;
                    bool duplicateY = y > 0 && sampledOldY[y - 1] == oldY;

                    if (thinVertical && duplicateX)
                    {
                        int prevIndex = y * newWidth + (x - 1);

                        // Keep wall on the side opposite the open area.
                        if (openLeft && !openRight)
                        {
                            // Open area is on left: keep rightmost wall cell.
                            newData[prevIndex] = preferredOpenTileId;
                        }
                        else if (openRight && !openLeft)
                        {
                            // Open area is on right: keep leftmost wall cell.
                            newData[index] = preferredOpenTileId;
                        }
                        else
                        {
                            // Ambiguous case: default to previous behavior.
                            newData[index] = preferredOpenTileId;
                        }
                    }

                    if (thinHorizontal && duplicateY)
                    {
                        int prevIndex = (y - 1) * newWidth + x;

                        // Keep wall on the side opposite the open area.
                        if (openUp && !openDown)
                        {
                            // Open area is above: keep lower wall cell.
                            newData[prevIndex] = preferredOpenTileId;
                        }
                        else if (openDown && !openUp)
                        {
                            // Open area is below: keep upper wall cell.
                            newData[index] = preferredOpenTileId;
                        }
                        else
                        {
                            // Ambiguous case: default to previous behavior.
                            newData[index] = preferredOpenTileId;
                        }
                    }
                }
            }

            // Guarantee closed borders to avoid visual gaps and out-of-map movement lanes.
            for (int x = 0; x < newWidth; x++)
            {
                newData[x] = topEdgeWallTileId;
                newData[(newHeight - 1) * newWidth + x] = bottomEdgeWallTileId;
            }
            for (int y = 0; y < newHeight; y++)
            {
                newData[y * newWidth] = leftEdgeWallTileId;
                newData[y * newWidth + (newWidth - 1)] = rightEdgeWallTileId;
            }

            // Apply explicit corner tiles so edge straightening does not leak corner artifacts.
            newData[0] = topLeftCornerTileId;
            newData[newWidth - 1] = topRightCornerTileId;
            newData[(newHeight - 1) * newWidth] = bottomLeftCornerTileId;
            newData[(newHeight - 1) * newWidth + (newWidth - 1)] = bottomRightCornerTileId;

            layer.data = JArray.FromObject(newData);
            layer.width = newWidth;
            layer.height = newHeight;
        }

        private bool IsOpenInSource(int[] sourceData, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return false;
            }

            int index = y * width + x;
            return openTileIds.Contains(sourceData[index]);
        }

        private int GetPreferredOpenTileId(int[] sourceData)
        {
            foreach (var tileId in sourceData)
            {
                if (openTileIds.Contains(tileId))
                {
                    return tileId;
                }
            }

            return openTileIds.FirstOrDefault();
        }

        private int GetPreferredWallTileId(int[] sourceData)
        {
            foreach (var tileId in sourceData)
            {
                if (tileId > 0 && !openTileIds.Contains(tileId))
                {
                    return tileId;
                }
            }

            return 1;
        }

        private int EnsureWallTile(int tileId, int fallbackWallTileId)
        {
            return (!openTileIds.Contains(tileId) && tileId > 0) ? tileId : fallbackWallTileId;
        }

        private int GetPreferredEdgeWallTileId(int[] sourceData, int width, int height, string edge, int fallbackWallTileId)
        {
            var counts = new Dictionary<int, int>();

            void AddIfWall(int tileId)
            {
                tileId = EnsureWallTile(tileId, fallbackWallTileId);
                if (!counts.ContainsKey(tileId))
                {
                    counts[tileId] = 0;
                }
                counts[tileId]++;
            }

            if (edge == "top" || edge == "bottom")
            {
                int y = edge == "top" ? 0 : height - 1;
                for (int x = 1; x < width - 1; x++)
                {
                    AddIfWall(sourceData[y * width + x]);
                }
            }
            else
            {
                int x = edge == "left" ? 0 : width - 1;
                for (int y = 1; y < height - 1; y++)
                {
                    AddIfWall(sourceData[y * width + x]);
                }
            }

            if (counts.Count == 0)
            {
                return fallbackWallTileId;
            }

            return counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        private int MapIndexCentered(int newIndex, int newSize, int oldSize)
        {
            if (newSize <= 0 || oldSize <= 0)
            {
                return 0;
            }

            double mapped = ((newIndex + 0.5) * oldSize / newSize) - 0.5;
            int oldIndex = (int)Math.Round(mapped);
            return Math.Max(0, Math.Min(oldIndex, oldSize - 1));
        }

        private void RebuildFloorMapFromLayerData(int width, int height)
        {
            if (Map?.layers == null || Map.layers.Count == 0)
            {
                return;
            }

            var floorData = (JArray)Map.layers[0].data;
            var newFloorMap = new bool[width][];
            for (int x = 0; x < width; x++)
            {
                newFloorMap[x] = new bool[height];
            }

            for (int i = 0; i < floorData.Count; i++)
            {
                int tileId = (int)floorData[i];
                int x = i % width;
                int y = i / width;
                if (y < height)
                {
                    newFloorMap[x][y] = openTileIds.Contains(tileId) &&
                        x > 0 &&
                        y > 0 &&
                        x < width - 1 &&
                        y < height - 1;
                }
            }

            FloorMap = newFloorMap;
        }

        private void RegenerateUnitPositions()
        {
            if (Units == null || FloorMap == null)
                return;

            var random = new Random();
            int width = Map.width;
            int height = Map.height;
            HashSet<int> unitPositions = new HashSet<int>();

            for (int i = 0; i < Units.Length; i++)
            {
                int x = random.Next(0, width);
                int y = random.Next(0, height);
                
                // Find a valid position for this unit
                int attempts = 0;
                while ((x >= FloorMap.Length || y >= FloorMap[0].Length || !FloorMap[x][y] || unitPositions.Contains((x << 16) | y)) && attempts < 100)
                {
                    x = random.Next(0, width);
                    y = random.Next(0, height);
                    attempts++;
                }
                
                if (attempts < 100)
                {
                    unitPositions.Add((x << 16) | y);
                    Units[i].X = x;
                    Units[i].Y = y;
                }
            }
        }
    }
}