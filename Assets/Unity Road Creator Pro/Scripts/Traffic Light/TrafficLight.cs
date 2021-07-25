using UnityEngine;

namespace RoadCreatorPro
{
    [HelpURL("https://mcrafterzz.itch.io/road-creator-pro")]
    public class TrafficLight : MonoBehaviour
    {

        public Material greenActive;
        public Material greenNonActive;
        public Material yellowActive;
        public Material yellowNonActive;
        public Material redActive;
        public Material redNonActive;

        public float greenTime = 30;
        public float yellowBeforeRedTime = 5;
        public float redTime = 60;
        public float yellowBeforeGreenTime = 1;
        public float startTime = 0;

        public float timeSinceLast = 0;
        public float nextChangeTick = 0;

        public enum TrafficLightColour { Green, YellowBeforeRed, Red, YellowBeforeGreen };
        public TrafficLightColour currentColour;
        public TrafficLightColour startColour;

        public bool paused = false;

        public void Start()
        {
            ModifyChangeTick();
        }

        public void Update()
        {
            if (paused == false)
            {
                timeSinceLast += Time.deltaTime;

                if (timeSinceLast >= nextChangeTick)
                {
                    currentColour = currentColour switch
                    {
                        TrafficLightColour.Green =>
                            TrafficLightColour.YellowBeforeRed,
                        TrafficLightColour.YellowBeforeRed =>
                            TrafficLightColour.Red,
                        TrafficLightColour.Red =>
                            TrafficLightColour.YellowBeforeGreen,
                        TrafficLightColour.YellowBeforeGreen =>
                             TrafficLightColour.Green,
                        _ => throw new System.Exception()
                    };

                    timeSinceLast = 0;
                    ModifyChangeTick();
                    UpdateMaterials();
                }
            }
        }

        public void ModifyChangeTick()
        {
            nextChangeTick = currentColour switch
            {
                TrafficLightColour.Green =>
                    greenTime,
                TrafficLightColour.YellowBeforeRed =>
                    yellowBeforeRedTime,
                TrafficLightColour.Red =>
                    redTime,
                TrafficLightColour.YellowBeforeGreen =>
                     yellowBeforeGreenTime,
                _ => throw new System.Exception(),
            };
        }

        public void UpdateMaterials()
        {
            Material[] materials = transform.GetComponent<MeshRenderer>().sharedMaterials;
            materials[4] = greenNonActive;
            materials[3] = yellowNonActive;
            materials[2] = redNonActive;

            switch (currentColour)
            {
                case TrafficLightColour.Green:
                    {
                        materials[4] = greenActive;
                        break;
                    }
                case TrafficLightColour.YellowBeforeRed | TrafficLightColour.YellowBeforeGreen:
                    {
                        materials[3] = yellowActive;
                        break;
                    }
                case TrafficLightColour.Red:
                    {
                        materials[2] = redActive;
                        break;
                    }
            }

            transform.GetComponent<MeshRenderer>().sharedMaterials = materials;
        }
    }
}