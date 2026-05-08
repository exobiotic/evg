using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using EvG.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace EvG.Controllers
{
    [Route("api/[controller]")]
    public class MapsController : Controller
    {
        [HttpGet]
        public string Get()
        {
            using (StreamReader sr = new StreamReader("./wwwroot/assets/maps/test.json"))
            {
                return sr.ReadToEnd();
            };
        }

        [HttpPost("generate")]
        public IActionResult Generate([FromBody] MapGeneratorInput input)
        {
            try
            {
                var map = MapGenerator.GenerateMap(input);
                return Ok(map);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("generate-and-save")]
        public IActionResult GenerateAndSave([FromBody] MapGeneratorInput input)
        {
            try
            {
                var map = MapGenerator.GenerateMap(input);
                string filename = input.Name.Replace(" ", "_").ToLower();
                MapGenerator.SaveMap(map, filename);
                return Ok(new { message = "Map generated and saved successfully", filename = filename, map = map });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("list")]
        public IActionResult List()
        {
            try
            {
                var maps = MapGenerator.GetAvailableMaps();
                return Ok(new { maps = maps });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("load/{filename}")]
        public IActionResult Load(string filename)
        {
            try
            {
                var map = MapGenerator.LoadMap(filename);
                return Ok(map);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("save-editor-map")]
        public IActionResult SaveEditorMap([FromBody] EditorMapSaveInput input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input.Filename))
                    return BadRequest(new { error = "Filename is required" });

                // Sanitise filename
                var safe = string.Concat(input.Filename.Split(Path.GetInvalidFileNameChars()));
                if (string.IsNullOrWhiteSpace(safe))
                    return BadRequest(new { error = "Invalid filename" });

                var mapBase = new FileInfo(System.Reflection.Assembly.GetAssembly(typeof(MapGenerator)).FullName)
                    ?.DirectoryName + "/wwwroot/assets/maps/";

                // Each map gets its own subfolder so the tilemap can reference tileset_complet.png by relative path
                var folder = Path.Combine(mapBase, safe);
                Directory.CreateDirectory(folder);

                var tilemapFilename = safe + ".json";
                var tilemapPath     = Path.Combine(folder, tilemapFilename);
                var descriptorPath  = Path.Combine(mapBase, safe + ".json");

                // Fix the tilemap reference inside the descriptor to point at the subfolder
                if (input.MapDescriptor != null)
                {
                    input.MapDescriptor["tilemap"] = safe + "/" + tilemapFilename;
                }

                System.IO.File.WriteAllText(tilemapPath,    Newtonsoft.Json.JsonConvert.SerializeObject(input.TilemapData,    Newtonsoft.Json.Formatting.Indented));
                System.IO.File.WriteAllText(descriptorPath, Newtonsoft.Json.JsonConvert.SerializeObject(input.MapDescriptor, Newtonsoft.Json.Formatting.Indented));

                // Copy the tileset into the subfolder if it isn't already there
                var tilesetSrc = Path.Combine(mapBase, "basic", "tileset_complet.png");
                var tilesetDst = Path.Combine(folder, "tileset_complet.png");
                if (System.IO.File.Exists(tilesetSrc) && !System.IO.File.Exists(tilesetDst))
                    System.IO.File.Copy(tilesetSrc, tilesetDst);

                return Ok(new { message = "Saved", filename = safe });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class EditorMapSaveInput
    {
        public string Filename { get; set; } = "";
        public Newtonsoft.Json.Linq.JObject? MapDescriptor { get; set; }
        public Newtonsoft.Json.Linq.JObject? TilemapData  { get; set; }
    }
}
