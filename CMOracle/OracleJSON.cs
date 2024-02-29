using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using UnityEngine;
using MoreSlugcats;
using static IteratorKit.CMOracle.OracleJSON;

namespace IteratorKit.CMOracle
{
    public class OracleJSON
    {
        public string id;
        public string roomId;
        public OracleBodyJson body = new OracleBodyJson();
        public float gravity;
        public float airFriction;
        public OracleRoomEffectsJson roomEffects;
        public int annoyedScore, angryScore;
        public float talkHeight = 250f;
        public Vector2 startPos = Vector2.zero;
        public string pearlFallback = null;
        public List<OracleJsonTilePos> cornerPositions = new List<OracleJsonTilePos>();
        public OverseerJson overseers;
        public UnityEngine.Color dialogColor = Color.white;

        public enum OracleType
        {
            normal, sitting
        }
        public OracleType type;
    
        //todo: deprecate this in favor of using the event slugcats to spawn in the iterator rather than forSlugList
        [JsonProperty("for")]
        private List<String> forSlugList = null; // keeps compiler quiet; base has these just to default values and msbuild hates that

        public List<SlugcatStats.Name> forSlugcats
        {
            get
            {
                List<SlugcatStats.Name> nameList = Expedition.ExpeditionData.GetPlayableCharacters();

                if (this.forSlugList != null && this.forSlugList.Count > 0)
                {
                    return new List<SlugcatStats.Name>(nameList.Where(x => forSlugList.Contains(x.value)));
                }
                else
                {
                    return nameList;
                }
            }
        }

        //OracleJSON -> slugcat name: OracleEventsJson -> generic/pearls/items: OracleEventObjectJson
        public Dictionary<string, OracleEventsJson> events = new Dictionary<string, OracleEventsJson>();

        public class OracleRoomEffectsJson
        {
            public int swarmers = 0;
            public string pearls = null;
        }

        public class OracleArmJson
        {
            public SpriteDataJson armColor = new SpriteDataJson();
            public SpriteDataJson armHighlight = new SpriteDataJson();
        }

        public class SpriteDataJson
        {
          //242: 
            // generic, used for a lot of things
            // values are not always used, usually just used for colors
            public float r, g, b = 0f;

            public float a { get; set; } = 255f;

            public string sprite;
            public string shader;

            public float scaleX, scaleY = -1f;

            public Color color
            {
                get { return new Color(r / 255, g / 255, b / 255, a / 255); }
            }
        }

        public class OracleBodyJson
        {
            public SpriteDataJson oracleColor, eyes, head, torso, arms, hands, legs, feet, chin, neck = new SpriteDataJson();
            public SpriteDataJson sigil = null;
            public OracleGownJson gown = new OracleGownJson();
            public OracleHaloJson halo = null;
            public OracleArmJson arm = null; 

            public class OracleGownJson
            {
                public OracleGownColorDataJson color = new OracleGownColorDataJson();

                public class OracleGownColorDataJson
                {
                    
                    public string type;
                    public float r, g, b, a = 255f;

                    public OracleGradientDataJson from;
                    public OracleGradientDataJson to;
                }

                //this only matters if type is gradient vs solid
                public class OracleGradientDataJson
                {
                    public float h, s, l = 0f;
                }

            }

            public class OracleHaloJson
            {
                public SpriteDataJson innerRing, outerRing, sparks = new SpriteDataJson();
            }
            
        }
        
        public class OracleEventsJson {             
            
            public List<OracleEventObjectJson> generic = new List<OracleEventObjectJson>();
            public List<OracleEventObjectJson> pearls = new List<OracleEventObjectJson>();
            public List<OracleEventObjectJson> items = new List<OracleEventObjectJson>();

            public class OracleEventObjectJson
            {

                [JsonProperty("event")]
                public string eventId;

                /// <exclude/>
                public string item
                {
                    set { this.eventId = value; }
                    get { return this.eventId; }
                }

                [JsonProperty("creatures")]
                private List<String> creaturesInRoomList = null;

                public List<CreatureTemplate.Type> creaturesInRoom
                {
                    get
                    {
                        List<CreatureTemplate.Type> creatures = new List<CreatureTemplate.Type>();
                        if (this.creaturesInRoomList == null || this.creaturesInRoomList?.Count <= 0)
                        {
                            return creatures;
                        }
                        foreach (string creature in this.creaturesInRoomList)
                        {
                            switch (creature.ToLower())
                            {
                                case "lizards":
                                    creatures.AddRange(allLizardsList);
                                    break;
                                case "vultures":
                                    creatures.AddRange(allVultures);
                                    break;
                                case "longlegs":
                                    creatures.AddRange(allLongLegsList);
                                    break;
                                case "bigcentipedes":
                                    creatures.AddRange(allBigCentipedes);
                                    break;
                                default:
                                    creatures.Add(new CreatureTemplate.Type(creature, false));
                                    break;
                            }

                            
                        }
                        return creatures;
                    }
                }

                public List<string> getTexts()
                {
                    
                    if ((this.texts?.Count ?? 0) == 0)
                    {
                        return new List<string>() { this.text };
                    }
                    if (this.random)
                    {
                        return new List<string>() { this.texts[UnityEngine.Random.Range(0, this.texts.Count())] };
                    }
                    return this.texts;


                }

                public int delay, hold = 10;

                public string text = null;
                public List<string> texts;
                
                //i have zero clue what below does. later me; explain?
                //later me here: i still have no idea. doesn't seem fundamental to the oracle speaking. later /later/ me, explain?
                
                public string translateString = null;
                
                public bool random = false;
                public float gravity = -50f; // -50f default value keeps gravity at whatever it already is
                public string sound; // links to SoundID
                public Vector2 moveTo;
                public string action;
                public string actionParam;

                public ChangePlayerScoreJson score;

                public UnityEngine.Color color = UnityEngine.Color.white;

                public string movement;
                public int pauseFrames = 0; // only for pebbles
                //todo: ^ for custom oracles
                
                public List<OracleScreenJson> screens = new List<OracleScreenJson>();


            }

            public class OracleScreenJson {
                public string image;
                public int hold;
                public float alpha = 255f;
                public Vector2 pos;
                public float moveSpeed = 50f;
            }

            public class ChangePlayerScoreJson
            {
                public string action; // set, add, subtract
                public int amount;
            }
        }

        public class OverseerJson
        {
            public SpriteDataJson color;
            public List<string> regions;
            public string guideToRoom;
            public int genMin, genMax;
        }

        private static readonly List<CreatureTemplate.Type> allLizardsList = new List<CreatureTemplate.Type>
        {
            CreatureTemplate.Type.LizardTemplate,
            CreatureTemplate.Type.PinkLizard,
            CreatureTemplate.Type.GreenLizard,
            CreatureTemplate.Type.BlueLizard,
            CreatureTemplate.Type.YellowLizard,
            CreatureTemplate.Type.WhiteLizard,
            CreatureTemplate.Type.RedLizard,
            CreatureTemplate.Type.BlackLizard,
            CreatureTemplate.Type.Salamander,
            CreatureTemplate.Type.CyanLizard,
            MoreSlugcatsEnums.CreatureTemplateType.SpitLizard,
            MoreSlugcatsEnums.CreatureTemplateType.EelLizard,
            MoreSlugcatsEnums.CreatureTemplateType.TrainLizard,
            MoreSlugcatsEnums.CreatureTemplateType.ZoopLizard
        };

        private static readonly List<CreatureTemplate.Type> allVultures = new List<CreatureTemplate.Type>
        {
            CreatureTemplate.Type.Vulture,
            CreatureTemplate.Type.KingVulture,
            MoreSlugcatsEnums.CreatureTemplateType.MirosVulture
        };

        private static readonly List<CreatureTemplate.Type> allLongLegsList = new List<CreatureTemplate.Type>
        {
            CreatureTemplate.Type.BrotherLongLegs,
            CreatureTemplate.Type.DaddyLongLegs,
            MoreSlugcatsEnums.CreatureTemplateType.TerrorLongLegs,
            MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy
        };

        private static readonly List<CreatureTemplate.Type> allBigCentipedes = new List<CreatureTemplate.Type>
        {
            CreatureTemplate.Type.Centipede,
            CreatureTemplate.Type.Centiwing,
            CreatureTemplate.Type.Centiwing,
            MoreSlugcatsEnums.CreatureTemplateType.AquaCenti
        };
    }


    
    
    public class OracleJsonTilePos
    {
        public int x, y;
    }

}
