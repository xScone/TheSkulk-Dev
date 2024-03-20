using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace MistEyes.src.Behaviours
{

    internal class MistEyesDirector : EnemyAI
    {
        private bool playerInfected;
        PlayerControllerB randomPlayer = StartOfRound.Instance.allPlayerScripts[UnityEngine.Random.Range(0, StartOfRound.Instance.allPlayerScripts.Length)];

		public override void Start()
		{
			base.Start();
			foreach (var player in StartOfRound.Instance.allPlayerScripts)
			{

			}
		}
		public override void Update()
        {
			base.Update();
            if (TimeOfDay.Instance.hour > 1 && playerInfected == false)
            {
                randomPlayer = StartOfRound.Instance.allPlayerScripts[UnityEngine.Random.Range(0, StartOfRound.Instance.allPlayerScripts.Length)];
                playerInfected = true;
                randomPlayer.gameObject.AddComponent<Infection>();
            }
            // TODO: Create a method that will remove infection script from players when the round ends
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player.GetComponent<Infection>() != null)
                {
                    player.GetComponent<Infection>().enabled = false;
                }
            }

        }
    }
}
