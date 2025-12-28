using System;
using UnityEngine;

namespace VisualAerobatics
{
    public class ModuleSmokeCanister : PartModule
    {
        // Persistent settings
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Burn Time"),
         UI_FloatRange(minValue = 30f, maxValue = 180f, stepIncrement = 10f)]
        public float burnTime = 60f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Smoke Color"),
         UI_ChooseOption(options = new[] { "White", "Red", "Blue", "Yellow", "Green", "Orange", "Purple", "Black" })]
        public string smokeColorName = "White";

        [KSPField(isPersistant = true, guiActive = true, guiName = "Time Remaining")]
        public float timeRemaining = 0f;

        [KSPField(isPersistant = true)]
        public bool hasActivated = false;

        [KSPField(isPersistant = true)]
        public bool isBurning = false;

        // Smoke particle system
        private ParticleSystem smokeParticles;
        private ParticleSystemRenderer smokeRenderer;

        // Color mapping
        private Color GetSmokeColor()
        {
            switch (smokeColorName)
            {
                case "Red": return new Color(1f, 0.2f, 0.2f, 0.8f);
                case "Blue": return new Color(0.2f, 0.4f, 1f, 0.8f);
                case "Yellow": return new Color(1f, 0.9f, 0.2f, 0.8f);
                case "Green": return new Color(0.2f, 0.9f, 0.3f, 0.8f);
                case "Orange": return new Color(1f, 0.5f, 0.1f, 0.8f);
                case "Purple": return new Color(0.7f, 0.2f, 0.9f, 0.8f);
                case "Black": return new Color(0.1f, 0.1f, 0.1f, 0.9f);
                default: return new Color(0.95f, 0.95f, 0.95f, 0.8f); // White
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (HighLogic.LoadedSceneIsFlight)
            {
                CreateSmokeSystem();

                // Resume burning if we were burning before scene change
                if (isBurning && timeRemaining > 0)
                {
                    StartSmoke();
                }
            }

            // Update time remaining display
            if (!hasActivated)
            {
                timeRemaining = burnTime;
            }
        }

        private void CreateSmokeSystem()
        {
            // Create GameObject for particles
            GameObject smokeObj = new GameObject("SmokeEffect");
            smokeObj.transform.parent = part.transform;
            smokeObj.transform.localPosition = Vector3.zero;
            smokeObj.transform.localRotation = Quaternion.identity;

            // Add particle system
            smokeParticles = smokeObj.AddComponent<ParticleSystem>();
            smokeRenderer = smokeObj.GetComponent<ParticleSystemRenderer>();

            // Configure main module
            var main = smokeParticles.main;
            main.startLifetime = 4f;
            main.startSpeed = 2f;
            main.startSize = 1.5f;
            main.startColor = GetSmokeColor();
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;
            main.gravityModifier = -0.05f; // Slight upward drift

            // Emission
            var emission = smokeParticles.emission;
            emission.rateOverTime = 40f;
            emission.enabled = false;

            // Shape - emit from a small cone
            var shape = smokeParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.1f;

            // Size over lifetime - grow as it disperses
            var sizeOverLife = smokeParticles.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 2f)
            ));

            // Color over lifetime - fade out
            var colorOverLife = smokeParticles.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = gradient;

            // Velocity over lifetime - add some turbulence
            var velocityOverLife = smokeParticles.velocityOverLifetime;
            velocityOverLife.enabled = true;
            velocityOverLife.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocityOverLife.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocityOverLife.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            // Use default particle material
            smokeRenderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            smokeRenderer.material.color = GetSmokeColor();

            // Initially stopped
            smokeParticles.Stop();
        }

        [KSPAction("Activate Smoke")]
        public void ActivateSmokeAction(KSPActionParam param)
        {
            ActivateSmoke();
        }

        [KSPEvent(guiActive = true, guiName = "Activate Smoke", guiActiveUnfocused = true, unfocusedRange = 5f)]
        public void ActivateSmoke()
        {
            if (hasActivated)
            {
                ScreenMessages.PostScreenMessage("Smoke canister already used!", 2f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            hasActivated = true;
            isBurning = true;
            timeRemaining = burnTime;

            // Hide the activate button
            Events["ActivateSmoke"].guiActive = false;

            StartSmoke();

            ScreenMessages.PostScreenMessage($"Smoke activated! {burnTime}s of {smokeColorName} smoke", 3f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void StartSmoke()
        {
            if (smokeParticles != null)
            {
                // Update color in case it was changed
                var main = smokeParticles.main;
                main.startColor = GetSmokeColor();
                smokeRenderer.material.color = GetSmokeColor();

                var emission = smokeParticles.emission;
                emission.enabled = true;
                smokeParticles.Play();
            }
        }

        private void StopSmoke()
        {
            isBurning = false;
            if (smokeParticles != null)
            {
                var emission = smokeParticles.emission;
                emission.enabled = false;
                smokeParticles.Stop();
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (!HighLogic.LoadedSceneIsFlight) return;

            if (isBurning && timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;

                if (timeRemaining <= 0)
                {
                    timeRemaining = 0;
                    StopSmoke();
                    ScreenMessages.PostScreenMessage("Smoke canister depleted!", 2f, ScreenMessageStyle.UPPER_CENTER);
                }
            }

            // Update color if changed mid-burn (shouldn't happen but just in case)
            if (isBurning && smokeParticles != null)
            {
                var main = smokeParticles.main;
                if (main.startColor.color != GetSmokeColor())
                {
                    main.startColor = GetSmokeColor();
                    smokeRenderer.material.color = GetSmokeColor();
                }
            }
        }

        // Support staging activation
        public override void OnActive()
        {
            base.OnActive();
            ActivateSmoke();
        }

        public override string GetInfo()
        {
            return "<b>Smoke Canister</b>\n\n" +
                   "Produces colored smoke for aerobatic displays.\n\n" +
                   "<b>Burn Time:</b> 30-180s (configurable)\n" +
                   "<b>Colors:</b> 8 options\n" +
                   "<b>Activation:</b> Staging or right-click";
        }

        private void OnDestroy()
        {
            if (smokeParticles != null)
            {
                Destroy(smokeParticles.gameObject);
            }
        }
    }
}
