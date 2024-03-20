using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace MistEyes.src.Behaviours
{
    internal class Infection : MonoBehaviour
    {
        public void Start()
        {
            GameNetworkManager.Instance.localPlayerController.DamagePlayer(50);
        }
        public void Update()
        {

        }
    }
}
