using BepInEx;
using R2API;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.PostProcessing;
using BepInEx.Configuration;
using RiskOfOptions.Options;
using RiskOfOptions;
using RoR2.Skills;
using RiskOfOptions.OptionConfigs;
using UnityEngine.Animations;
using System.IO;
using System.Reflection;
using MonoMod.Cil;
using HarmonyLib;
using RoR2.CameraModes;
using RoR2.UI;
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: HG.Reflection.SearchableAttribute.OptIn]
[assembly: HG.Reflection.SearchableAttribute.OptInAttribute]
namespace TrueFirstPerson
{
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [System.Serializable]
    [BepInDependency("com.weliveinasociety.CustomEmotesAPI", BepInDependency.DependencyFlags.SoftDependency)]
    public class Main : BaseUnityPlugin
    {
        public static BepInEx.PluginInfo PInfo { get; private set; }
        public const string ModGuid = "com.brynzananas.truefirstperson";
        public const string ModName = "True First Person";
        public const string ModVer = "1.0.0";
        public static ConfigEntry<KeyboardShortcut> FirstPersonToggle;
        public static ConfigEntry<float> FieldOfViewConfig;
        public static ConfigEntry<float> CurrentClip;
        public static ConfigEntry<bool> EnableLook;
        public static ConfigEntry<bool> EmoteLookLockMode;
        //public static ConfigEntry<bool> EnableEmoteLookLock;
        //public static ConfigEntry<bool> EnableCharacterRotation;
        public static ConfigEntry<bool> EnableDebugKeys;

        public void Awake()
        {
            FirstPersonToggle = Config.Bind<KeyboardShortcut>("General", "First Person Toggle", new KeyboardShortcut(KeyCode.O), "Key to toggle first person");
            FieldOfViewConfig = Config.Bind<float>("General", "Field Of View", 90f, "I don't need to explain this");
            CurrentClip = Config.Bind<float>("General", "Current Camera Near Clip Parameter", 0.3f, "Current value of near camera clipping");
            EnableLook = Config.Bind<bool>("General", "Enable Input Camera Direction?", true, "Enable first person camera rotation to follow input rotation?");
            //EnableEmoteLookLock = Config.Bind<bool>("General", "Enable Emote Camera Lock?", true, "Enable locking camera rotation while emoting?");
            EmoteLookLockMode = Config.Bind<bool>("General", "Emote Camera Lock Mode", true, "Enable first person camera rotation to follow head rotation while emoting?");
            //EnableCharacterRotation = Config.Bind<bool>("General", "Enable Character Rotation?", true, "Rotate character everytime while in first person?");
            EnableDebugKeys = Config.Bind<bool>("General", "Enable Control Keys?", true, "Enable keys to control FOV and Near Clip values in game?\n\nUse +/- buttons to increase/decrease FOV value. Hold Shift to change Near Clip value");
            survivorDefaultBoneOverride.Add("RobPaladinBody", "Armature/base/spine.001/spine.002/spine.003/spine.004/neck/head");
            survivorDefaultBoneOverride.Add("HuntressBody", "HuntressArmature/ROOT/base/stomach/chest/head");
            emotesEnabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(EmoteCompatability.customEmotesApiGUID);
            On.RoR2.PlayerCharacterMasterController.Update += SetCamera;
            On.RoR2.PlayerCharacterMasterController.OnBodyStart += PlayerCharacterMasterController_OnBodyStart;
            On.RoR2.SurvivorCatalog.Init += SurvivorCatalog_Init;
            survivorClipDefaultValues.Add("Bandit2Body", 0.21f);
            survivorClipDefaultValues.Add("CommandoBody", 0.03337884f);
            survivorClipDefaultValues.Add("CrocoBody", 0.5f);
            survivorClipDefaultValues.Add("HereticBody", 0.755f);
            survivorClipDefaultValues.Add("LoaderBody", 0.06564139f);
            survivorClipDefaultValues.Add("MageBody", 0.03318632f);
            survivorClipDefaultValues.Add("MercBody", 0.0266057f);
            survivorClipDefaultValues.Add("ToolbotBody", 0.51f);
            survivorClipDefaultValues.Add("TreebotBody", 1f);
            survivorClipDefaultValues.Add("RailgunnerBody", 0.2f);
            survivorClipDefaultValues.Add("VoidSurvivorBody", 0.2f);
            survivorClipDefaultValues.Add("RobPaladinBody", 0.3795359f);
            survivorClipDefaultValues.Add("EngiBody", 0.01989561f);
            survivorClipDefaultValues.Add("HuntressBody", 0.2203578f);
            survivorClipDefaultValues.Add("BidenBody", 0.1532053f);
            survivorClipDefaultValues.Add("CaptainBody", 0.01986642f);

            ModSettingsManager.AddOption(new KeyBindOption(FirstPersonToggle));
            ModSettingsManager.AddOption(new FloatFieldOption(FieldOfViewConfig));
            ModSettingsManager.AddOption(new FloatFieldOption(CurrentClip));
            ModSettingsManager.AddOption(new CheckBoxOption(EnableLook));
            ModSettingsManager.AddOption(new CheckBoxOption(EmoteLookLockMode));
            //ModSettingsManager.AddOption(new CheckBoxOption(EnableEmoteLookLock));
            //ModSettingsManager.AddOption(new CheckBoxOption(EnableCharacterRotation));
            ModSettingsManager.AddOption(new CheckBoxOption(EnableDebugKeys));
            ModSettingsManager.AddOption(new GenericButtonOption("Save Near Clip Value", "General", SaveValue));
            FieldOfViewConfig.SettingChanged += FieldOfViewConfig_SettingChanged;
            CurrentClip.SettingChanged += FieldOfViewConfig_SettingChanged;
            EnableLook.SettingChanged += EnableLook_SettingChanged;
            var texture = new Texture2D(256, 256);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TrueFirstPerson.modicon.png"))
            using (var reader = new BinaryReader(stream))
            {
                texture.LoadImage(reader.ReadBytes((int)stream.Length));
                var sprite = Sprite.Create(texture, new Rect(0, 0, 256, 256), new Vector2(0f, 0f));
                ModSettingsManager.SetModIcon(sprite);
            }
        }

        private void PlayerCharacterMasterController_OnBodyStart(On.RoR2.PlayerCharacterMasterController.orig_OnBodyStart orig, PlayerCharacterMasterController self)
        {
            orig(self);
            toggle = true;
        }

        private static bool emotesEnabled = false;
        private void SaveValue()
        {
            if (camera)
            {
                string bodyName = camera.transform.root.GetComponent<CharacterModel>().body.name;
                while (bodyName.Contains("(Clone)"))
                    bodyName = bodyName.Replace("(Clone)", "");
                survivorClipValues[bodyName].Value = CurrentClip.Value;
            }
        }
        private void EnableLook_SettingChanged(object sender, EventArgs e)
        {
            if (camera)
            {
                camera.transform.localRotation = Quaternion.identity;
            }
        }

        private void FieldOfViewConfig_SettingChanged(object sender, EventArgs e)
        {
            if (camera)
            {
                Camera cameraComponent = camera.GetComponent<Camera>();
                previousMainCameraRigController.baseFov = FieldOfViewConfig.Value;
                cameraComponent.nearClipPlane = CurrentClip.Value;
            }
        }
        public static Dictionary<string, string> survivorDefaultBoneOverride = new Dictionary<string, string>();
        public static Dictionary<string, ConfigEntry<string>> survivorBoneOverride = new Dictionary<string, ConfigEntry<string>>();
        public static Dictionary<string, float> survivorClipDefaultValues = new Dictionary<string, float>();
        public static Dictionary<string, ConfigEntry<float>> survivorClipValues = new Dictionary<string, ConfigEntry<float>>();
        private bool set = false;
        private void SurvivorCatalog_Init(On.RoR2.SurvivorCatalog.orig_Init orig)
        {
            orig();
            if (!set)
            {
                set = true;
                foreach (var survivor in SurvivorCatalog.allSurvivorDefs)
                {
                    string survivorName = survivor.bodyPrefab.name;
                    ConfigEntry<float> SurvivorClip;
                    ConfigEntry<string> PathOverride;
                    float defaultValue = 0.3f;
                    if (survivorClipDefaultValues.ContainsKey(survivorName))
                    {
                        defaultValue = survivorClipDefaultValues[survivorName];
                    }
                    string path = "";
                    if (survivorDefaultBoneOverride.ContainsKey(survivorName))
                    {
                        path = survivorDefaultBoneOverride[survivorName];
                    }
                    survivorName = survivorName.Replace("Body", "");
                    survivorName = CleanUpString(survivorName);
                    SurvivorClip = Config.Bind<float>("Survivors Config", survivorName + " Clip Value", defaultValue, "Near clip value for " + survivorName + " survivor");
                    ModSettingsManager.AddOption(new FloatFieldOption(SurvivorClip));
                    survivorClipValues.Add(survivor.bodyPrefab.name, SurvivorClip);

                    PathOverride = Config.Bind<string>("Survivors Config", survivorName + " Path Override", path, "Manual camera parenting path override");
                    ModSettingsManager.AddOption(new StringInputFieldOption(PathOverride));
                    survivorBoneOverride.Add(survivor.bodyPrefab.name, PathOverride);
                    //PathOverride.SettingChanged += PathOverride_SettingChanged;
                }
            }
        }

        //private void PathOverride_SettingChanged(object sender, EventArgs e)
        //{
        //    if (camera)
        //    {
        //        try
        //        {
        //            SetHead();
        //        }
        //        catch
        //        {
        //            return;
        //        }
                
        //    }
        //}
        public void SetHead(CharacterBody body = null)
        {
            if (body == null)
            {
                body = NetworkUser.readOnlyLocalPlayersList[0].master?.GetBody();
            }
            if (body == null) return;
            string bodyName = body.name;
            while (bodyName.Contains("(Clone)"))
                bodyName = bodyName.Replace("(Clone)", "");
            Transform bodyTransform = body.GetComponent<ModelLocator>().modelTransform;
            HurtBoxGroup hurtBoxGroup = bodyTransform.GetComponent<HurtBoxGroup>();
            if (survivorBoneOverride.ContainsKey(bodyName) && survivorBoneOverride[bodyName].Value != "")
            {
                try
                {
                    head = bodyTransform.Find(survivorBoneOverride[bodyName].Value).gameObject;
                }
                catch
                {
                }

            }
            if (head == null)
                foreach (var hurtbox in hurtBoxGroup.hurtBoxes)
                {
                    if (hurtbox.isSniperTarget && hurtbox != hurtBoxGroup.mainHurtBox)
                    {
                        head = hurtbox.gameObject;
                        break;
                    }
                }
            if (bodyTransform.GetComponent<ChildLocator>() == null) return;
            if (head == null)
            {
                foreach (var bone in bodyTransform.GetComponent<ChildLocator>().transformPairs)
                {
                    //Debug.Log(bone.name);
                    if (bone.name.ToLower().Contains("head") && !bone.name.ToLower().Contains("cannon"))
                    {
                        head = bone.transform.gameObject; break;
                    }
                }
            }

            if (head == null)
            {
                foreach (var bone in bodyTransform.GetComponent<ChildLocator>().transformPairs)
                {
                    //Debug.Log(bone.name);
                    if (bone.name.ToLower().Contains("chest"))
                    {
                        head = bone.transform.gameObject; break;
                    }
                }
            }
        }
        public void SetCameraLocalPosition()
        {
            Vector3 vector3 = Vector3.zero;
            if (head.GetComponent<HurtBox>())
            {
                //Debug.Log("Got Hurtbox");
                if (head.GetComponent<HurtBox>().GetComponent<SphereCollider>())
                {
                    //Debug.Log("Got Hurtbox Sphere");
                    vector3 = head.GetComponent<HurtBox>().GetComponent<SphereCollider>().center;
                }
                if (head.GetComponent<CapsuleCollider>())
                {
                    //Debug.Log("Got Hurtbox Capsule");
                    vector3 = head.GetComponent<CapsuleCollider>().center;
                }
            }
            if (head.GetComponent<SphereCollider>())
            {
                //Debug.Log("Got Sphere");
                vector3 = head.GetComponent<SphereCollider>().center;
            }
            if (head.GetComponent<CapsuleCollider>())
            {
                //Debug.Log("Got Capsule");
                vector3 = head.GetComponent<CapsuleCollider>().center;
            }
            //Debug.Log("Final Vector: " + vector3);
            camera.transform.localPosition = vector3;
        }
        public string CleanUpString(string stringName)
        {
            char[] forbiddenStuff = new char[] {'[', ']', '\"', '\''};
            foreach (char c in forbiddenStuff)
                stringName = stringName.Replace(c, ' ');
            return stringName;
        }
        float angle = -69420f;
        Quaternion initialQuaternion = Quaternion.identity;
        Vector3 initialVector = Vector3.zero;
        bool emoteAngles = false;
        
        private void SetCamera(On.RoR2.PlayerCharacterMasterController.orig_Update orig, PlayerCharacterMasterController self)
        {
            orig(self);
            if (Input.GetKeyDown(FirstPersonToggle.Value.MainKey) && !PauseManager.isPaused && !self.networkUser.cameraRigController.hud.GetComponentInChildren<ChatBox>().showInput && self.body)
            {
                if (previousMainCamera == null)
                {
                    previousMainCamera = self.networkUser.cameraRigController.gameObject;
                    previousMainCameraRigController = self.networkUser.cameraRigController;
                }
                if (eventSystem == null)
                {
                    eventSystem = previousMainCameraRigController.hud.GetComponent<MPEventSystemProvider>().eventSystem;
                }
                if (previousCamera == null)
                    previousCamera = previousMainCameraRigController.sceneCam.gameObject;
                if (musicListener == null)
                    musicListener = previousCamera.transform.Find("MusicListener").gameObject;

                if (toggle)
                {
                    Transform bodyTransform = self.body.GetComponent<ModelLocator>().modelTransform;
                    HurtBoxGroup hurtBoxGroup = bodyTransform.GetComponent<HurtBoxGroup>();
                    string bodyName = self.body.name;
                    while (bodyName.Contains("(Clone)"))
                        bodyName = bodyName.Replace("(Clone)", "");
                    SetHead(self.body);
                    /*
                    while (bodyName.Contains("(Clone)"))
                        bodyName = bodyName.Replace("(Clone)", "");
                    if (survivorBoneOverride.ContainsKey(bodyName) && survivorBoneOverride[bodyName] != "")
                    {
                        try
                        {
                            head = bodyTransform.Find(survivorBoneOverride[bodyName]).gameObject;
                        }
                        catch
                        {
                            return;
                        }
                        
                    }
                    if (head == null)
                        foreach (var hurtbox in hurtBoxGroup.hurtBoxes)
                        {
                            if (hurtbox.isSniperTarget && hurtbox != hurtBoxGroup.mainHurtBox)
                            {
                                head = hurtbox.gameObject;
                                break;
                            }
                        }
                    if (bodyTransform.GetComponent<ChildLocator>() == null) return;
                    if (head == null)
                    {
                        foreach(var bone in bodyTransform.GetComponent<ChildLocator>().transformPairs)
                        {
                            //Debug.Log(bone.name);
                            if (bone.name.ToLower().Contains("head") && !bone.name.ToLower().Contains("cannon"))
                            {
                                head = bone.transform.gameObject; break;
                            }
                        }
                    }
                    
                    if (head == null)
                    {
                        foreach (var bone in bodyTransform.GetComponent<ChildLocator>().transformPairs)
                        {
                            //Debug.Log(bone.name);
                            if (bone.name.ToLower().Contains("chest"))
                            {
                                head = bone.transform.gameObject; break;
                            }
                        }
                    }
                    if (head == null)
                    {
                        foreach (var bone in bodyTransform.GetComponent<ChildLocator>().transformPairs)
                        {
                            //Debug.Log(bone.name);
                            if (bone.name.ToLower().Contains("chest"))
                            {
                                head = bone.transform.gameObject; break;
                            }
                        }
                    }*/
                    
                    if (head == null)
                    {
                        if (mainCamera)
                        {
                            //Destroy(camera);
                            //Destroy(UIcamera);
                        }

                        return;
                    }
                    if (camera == null)
                    {
                        camera = Instantiate(previousCamera, head.transform);
                        SetCameraLocalPosition();
                        camera.GetComponent<SceneCamera>().cameraRigController = previousMainCameraRigController;


                        var component = camera.AddComponent<FirstPersonCameraComponent>();
                        
                    }
                    //camera.transform.SetParent(head.transform);
                    //camera.transform.localPosition = Vector3.zero;
                    camera.transform.localRotation = Quaternion.identity;
                    //camera.GetComponent<FirstPersonCameraComponent>().previousPosition = self.body.aimOriginTransform.localPosition;
                    //camera.GetComponent<FirstPersonCameraComponent>().transformToChange = self.body.aimOriginTransform;
                    //self.body.aimOriginTransform.position = head.transform.position;
                    //rotationCamera.x = previousMainCamera.transform.localRotation.eulerAngles.x;
                    //rotationCamera.y = previousMainCamera.transform.localRotation.eulerAngles.y;
                    //previousMainCameraRigController.firstPersonTarget = camera;
                    //self.body.came
                    Camera cameraComponent = camera.GetComponent<Camera>();
                    cameraComponent.fieldOfView = FieldOfViewConfig.Value;
                    float nearClipValue = 0.3f;
                    if (survivorClipValues.ContainsKey(bodyName))
                    {
                        nearClipValue = survivorClipValues[bodyName].Value;
                    }
                    cameraComponent.nearClipPlane = nearClipValue;
                    CurrentClip.Value = nearClipValue;
                    toggle = false;
                }
                else
                {
                    if (camera)
                    {
                        Destroy(camera);
                        //Destroy(UIcamera);
                    }
                    toggle = true;
                }
            }
            if (camera)
            {
                //Quaternion quaternion = new Quaternion();
                //bool emote = false;
                //if (emotesEnabled && EmoteLookLockMode.Value)
                //{
                    
                //    if (EmoteCompatability.CurrentEmote() != "none")
                //    {
                //        emote = true;
                //        //Vector3 vector3 = camera.transform.parent.rotation.eulerAngles;
                //        //if (!emoteAngles)
                //        //{
                //        //    angle = vector3.x;
                //        //    initialVector = camera.transform.root.forward - vector3;
                //        //    initialQuaternion = camera.transform.parent.rotation;
                //        //    emoteAngles = true;
                //        //}
                //        //float finalangle = vector3.x - angle;
                //        //if (finalangle < 0)
                //        //    finalangle = 360 - finalangle;
                //        //if (finalangle > 360)
                //        //    finalangle = finalangle - 360;
                //        //Chat.AddMessage("Parent: X" + vector3.x);
                //        //Chat.AddMessage("Initial X:" + angle);
                //        //Chat.AddMessage("Combined X:" +(finalangle));
                //        quaternion = EmoteCompatability.GetHeadRotation();
                //        if (EmoteLookLockMode.Value)
                //        camera.transform.rotation = quaternion;

                //    }
                //    else
                //    {
                //        //if(emoteAngles)
                //        //    emoteAngles = false;
                //    }
                //}
                if (EnableLook.Value && head && head.activeInHierarchy)
                {
                    //camera.transform.position = head.transform.position;
                    //rotationCamera.x += self.networkUser.inputPlayer.GetAxis(2) * self.networkUser.localUser.userProfile.mouseLookSensitivity;
                    //rotationCamera.y += self.networkUser.inputPlayer.GetAxis(3) * self.networkUser.localUser.userProfile.mouseLookSensitivity;
                    //rotationCamera.y = Mathf.Clamp(rotationCamera.y, -88f, 88f);
                    //var xQuat = Quaternion.AngleAxis(rotationCamera.x, Vector3.up);
                    //var yQuat = Quaternion.AngleAxis(rotationCamera.y, Vector3.left);
                    
                    //if (previousMainCamera && !emote || EmoteLookLockMode.Value != 0)
                    //{
                    //    camera.transform.rotation = previousMainCamera.transform.rotation;
                    //    //previousMainCamera.transform.rotation = xQuat * yQuat;
                    //}
                    self.body.inputBank.aimDirection = camera.transform.forward;
                    //self.body.inputBank.moveVector = Quaternion.AngleAxis(rotationCamera.x, Vector3.up) * new Vector3(self.body.inputBank.rawMoveData.x, 0, self.body.inputBank.rawMoveData.y);
                    //previousMainCamera.transform.rotation = camera.transform.rotation;
                    //self.body.characterMotor.characterDirection.targetTransform = camera.transform;
                    /*
                    if (emote && EmoteLookLockMode.Value == 1)
                    {
                        camera.transform.rotation = Quaternion.LookRotation(self.body.inputBank.aimDirection) * quaternion;
                    }
                    else if (!emote || EmoteLookLockMode.Value != 0)
                    {
                        camera.transform.rotation = Quaternion.LookRotation(self.body.inputBank.aimDirection);
                    }
                    */
                    //if (EnableCharacterRotation.Value)
                    //    self.body.characterDirection.targetTransform.rotation = Quaternion.LookRotation(new Vector3(self.body.inputBank.aimDirection.x, 0, self.body.inputBank.aimDirection.z));
                    //self.body.characterDirection.Simulate(0.1f);
                }
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus))
                        Increment(CurrentClip, Time.deltaTime);
                    if (Input.GetKey(KeyCode.Minus))
                        Increment(CurrentClip, -Time.deltaTime);
                }
                else
                {
                    if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus))
                        Increment(FieldOfViewConfig, Time.deltaTime * 15);
                    if (Input.GetKey(KeyCode.Minus))
                        Increment(FieldOfViewConfig, -Time.deltaTime * 15);
                }
                void Increment(ConfigEntry<float> config, float number)
                {
                    config.Value += number;
                }
                
            };
        }
        public static MPEventSystem eventSystem = null;
        public static Vector2 rotationCamera = Vector2.zero;
        public static GameObject previousCamera = null;
        public static GameObject head = null;
        public static GameObject camera = null;
        public static GameObject UIcamera = null;
        public static GameObject previousMainCamera = null;
        public static CameraRigController previousMainCameraRigController = null;
        public static GameObject mainCamera = null;
        public static GameObject musicListener = null;
        //public static CameraState cameraState = new CameraState { fov = 90, position = Vector3.zero, rotation = Quaternion.identity};
        public bool toggle = true;
        public class FirstPersonCameraComponent : MonoBehaviour
        {
            private float previousFov = 60f;
            private Vector3 previousPosition = Vector3.zero;
            public Transform transformToChange;
            private Transform previousTransform;
            //private CharacterCameraParamsData characterCameraParamsData;
            //public GameObject camera;
            public void OnEnable()
            {
                if (previousCamera)
                {
                    previousMainCameraRigController.sceneCam = camera.GetComponent<Camera>();
                    previousFov = previousMainCameraRigController.baseFov;
                    previousMainCameraRigController.baseFov = FieldOfViewConfig.Value;
                    transformToChange = transform.root.GetComponent<CharacterModel>().body.aimOriginTransform;
                    previousPosition = transformToChange.localPosition;
                    previousTransform = transformToChange.parent;
                    transformToChange.SetParent(transform, false);
                    transformToChange.localPosition = Vector3.zero;
                    musicListener.transform.parent = null;
                    previousCamera.SetActive(false);
                    //rotationCamera.x = previousMainCamera.transform.localRotation.eulerAngles.x;
                    //rotationCamera.y = previousMainCamera.transform.localRotation.eulerAngles.y;
                    //transform.localRotation = previousMainCamera.transform.localRotation;
                    //characterCameraParamsData = new CharacterCameraParamsData
                    //{
                    //    fov = previousFov,
                    //    idealLocalCameraPos = Vector3.zero,
                    //    isFirstPerson = true,
                    //    maxPitch = 0,
                    //    minPitch = 0,
                    //    wallCushion = 0,
                    //    overrideFirstPersonFadeDuration = 0,
                    //    pivotVerticalOffset = 0,
                    //};
                    //previousMainCameraRigController.targetBody.GetComponent<CameraTargetParams>().AddParamsOverride(new CameraTargetParams.CameraParamsOverrideRequest
                    //{
                    //    cameraParamsData = characterCameraParamsData,
                    //    priority = 64,
                    //}, 0);
                }
                    
            }
            //public void Update()
            //{
            //    if (previousMainCamera)
            //    {
            //        transform.rotation = previousMainCamera.transform.rotation;
            //    }
            //}

            public void LateUpdate()
            {
                if (emotesEnabled && EmoteLookLockMode.Value && EmoteCompatability.CurrentEmote() != "none")
                {
                    transform.rotation = EmoteCompatability.GetHeadRotation();
                }
                else
                {
                    if (EnableLook.Value && previousMainCamera)
                    {
                        transform.rotation = previousMainCamera.transform.rotation;
                    }
                }
                    
            }
            //public void EarlyUpdate()
            //{
            //    if (previousMainCamera)
            //    {
            //        transform.rotation = previousMainCamera.transform.rotation;
            //    }
            //}
            public void OnDisable()
            {
                if (previousCamera)
                {
                    previousMainCameraRigController.sceneCam = previousCamera.GetComponent<Camera>();
                    previousMainCameraRigController.baseFov = previousFov;
                    //previousMainCameraRigController.targetBody.GetComponent<CameraTargetParams>().RemoveParamsOverride(characterCameraParamsData);
                    musicListener.transform.parent = previousCamera.transform;
                    previousCamera.SetActive(true);
                    //previousMainCamera.transform.localRotation = transform.localRotation;
                }
                if (transformToChange)
                {
                    transformToChange.SetParent (previousTransform, false);
                    transformToChange.localPosition = previousPosition;
                    
                }  
            }
        }
    }
    
}