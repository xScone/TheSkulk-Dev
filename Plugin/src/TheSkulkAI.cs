using System;
using System.Collections;
using System.Diagnostics;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MistEyes {

    // You may be wondering, how does the Example Enemy know it is from class MistEyesAI?
    // Well, we give it a reference to to this class in the Unity project where we make the asset bundle.
    // Asset bundles cannot contain scripts, so our script lives here. It is important to get the
    // reference right, or else it will not find this file. See the guide for more information.

    class TheSkulkAI : EnemyAI
	{
        // We set these in our Asset Bundle, so we can disable warning CS0649:
        // Field 'field' is never assigned to, and will always have its default value 'value'
        #pragma warning disable 0649
        public Transform turnCompass;
        public Transform attackArea;
        #pragma warning restore 0649
        float timeSinceHittingLocalPlayer;
        float timeSinceNewRandPos;
		Transform lastKnownPlayerPos;
		bool gotLastPos;
        Vector3 positionRandomness;
        Vector3 StalkPos;
		float chaseTimer;
		LineRenderer line;
		System.Random enemyRandom;
        bool isDeadAnimationDone;
        enum State {
            SearchingForPlayer,
			StaringAtPlayers,
			ChasePlayer,
			GoToLastKnown,
		}

        [Conditional("DEBUG")]
        void LogIfDebugBuild(string text) {
            Plugin.Logger.LogInfo(text);
        }

        public override void Start() {
            base.Start();
			LogIfDebugBuild("Example Enemy Spawned");
			#if DEBUG
			line = gameObject.AddComponent<LineRenderer>();
			line.widthMultiplier = 0.2f; // reduce width of the line
			#endif
			timeSinceHittingLocalPlayer = 0;
            creatureAnimator.SetTrigger("startWalk");
            timeSinceNewRandPos = 0;
            positionRandomness = new Vector3(0, 0, 0);
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            isDeadAnimationDone = false;
			// NOTE: Add your behavior states in your enemy script in Unity, where you can configure fun stuff
			// like a voice clip or an sfx clip to play when changing to that specific behavior state.

			// We make the enemy start searching. This will make it start wandering around.
			StartSearch(transform.position);
			currentBehaviourStateIndex = (int)State.SearchingForPlayer;
			
		}

        public override void Update() {
            base.Update();
			/*/if (enemyHP != null)
			{
				Plugin.Logger.LogInfo(enemyHP);
			}/*/
			if (isEnemyDead){
                // For some weird reason I can't get an RPC to get called from HitEnemy() (works from other methods), so we do this workaround. We just want the enemy to stop playing the song.
                if(!isDeadAnimationDone){ 
                    LogIfDebugBuild("Stopping enemy voice with janky code.");
                    isDeadAnimationDone = true;
                    creatureVoice.Stop();
                    creatureVoice.PlayOneShot(dieSFX);
                }
                return;
            }
            timeSinceHittingLocalPlayer += Time.deltaTime;
            timeSinceNewRandPos += Time.deltaTime;
            var state = currentBehaviourStateIndex;
            if (stunNormalizedTimer > 0f)
            {
                agent.speed = 0f;
            }
        }
		public static IEnumerator DrawPath(LineRenderer line, NavMeshAgent agent)
		{
			if (!agent.enabled) yield break;
			yield return new WaitForEndOfFrame();
			line.SetPosition(0, agent.transform.position); //set the line's origin

			line.positionCount = agent.path.corners.Length; //set the array of positions to the amount of corners
			for (var i = 1; i < agent.path.corners.Length; i++)
			{
				line.SetPosition(i, agent.path.corners[i]); //go through each corner and set that to the line renderer's position
			}
		}
		public override void DoAIInterval() {
            
            base.DoAIInterval();
			StartCoroutine(DrawPath(line, agent));
			
			if (isEnemyDead || StartOfRound.Instance.allPlayersDead) {
                return;
            };
			switch(currentBehaviourStateIndex) {
				case (int)State.SearchingForPlayer:
					agent.speed = 1f;
					if (FoundClosestPlayerInRange(40f, 10f) || (FoundClosestPlayerInRange(80f, 10f) && HasLineOfSightToPosition(targetPlayer.transform.position)))
					{
						StopSearch(currentSearch);
						SwitchToBehaviourClientRpc((int)State.ChasePlayer);
					}
				break;
				case (int)State.StaringAtPlayers:
					agent.speed = 0f;
					if (FoundClosestPlayerInRange(40f, 0f) && HasLineOfSightToPosition(targetPlayer.transform.position))
					{
						turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
						transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
						LogIfDebugBuild("Start Target Player");
						GetLastKnownPos();
					}
					if (Vector3.Distance(transform.position, targetPlayer.transform.position) > 10 && !HasLineOfSightToPosition(targetPlayer.transform.position))
					{
						//change to going to last known, if player not found then switch to searching.
						GetLastKnownPos();
						StartSearch(transform.position);
						SwitchToBehaviourClientRpc((int)State.GoToLastKnown);
					}
				break;

					
				case (int)State.ChasePlayer:
					agent.speed = 5f;
					// Keep targetting closest player, unless they are over 20 units away and we can't see them.
					if (Vector3.Distance(transform.position, targetPlayer.transform.position) > 20 && !HasLineOfSightToPosition(targetPlayer.transform.position))
					{
						LogIfDebugBuild("Stop Target Player");
						//change to going to last known, if player not found then switch to searching.
						StartSearch(transform.position);
						SwitchToBehaviourClientRpc((int)State.GoToLastKnown);
						return;
					}
					if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 20 && HasLineOfSightToPosition(targetPlayer.transform.position))
					{
						StartSearch(transform.position);
						SwitchToBehaviourClientRpc((int)State.StaringAtPlayers);
					}

					positionRandomness = new Vector3(enemyRandom.Next(-2, 2), 0, enemyRandom.Next(-2, 2));
					StalkPos = targetPlayer.transform.position - Vector3.Scale(new Vector3(-5, 0, -5), targetPlayer.transform.forward) + positionRandomness;
					//StartSearch(targetPlayer.transform.position);
					SetDestinationToPosition(StalkPos, checkForPath: false);
				break;
				case (int)State.GoToLastKnown:
					
					agent.speed = 2.5f;
					positionRandomness = new Vector3(enemyRandom.Next(-2, 2), 0, enemyRandom.Next(-2, 2));
					chaseTimer += Time.deltaTime;
					StalkPos = lastKnownPlayerPos.position; //- Vector3.Scale(new Vector3(-5, 0, -5), lastKnownPlayerPos.forward) + positionRandomness;
					SetDestinationToPosition(lastKnownPlayerPos.position, checkForPath: false);
					if (FoundClosestPlayerInRange(40f, 10f) && HasLineOfSightToPosition(targetPlayer.transform.position) || FoundClosestPlayerInRange(20f, 5f))
					{
						chaseTimer = 0;
						SwitchToBehaviourClientRpc((int)State.ChasePlayer);
					}
					else if (chaseTimer > 5f)
					{
						StartSearch(transform.position);
						chaseTimer = 0;
						SwitchToBehaviourClientRpc((int)State.SearchingForPlayer);
					}


				break;

				default:
					LogIfDebugBuild("This Behavior State doesn't exist!");
				break;
			}
		}
		void GetLastKnownPos()
		{
			if (!gotLastPos) 
			{
				lastKnownPlayerPos = targetPlayer.transform;
				gotLastPos = true;
			}
		}
        bool FoundClosestPlayerInRange(float range, float senseRange) {
            TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: true);
            if(targetPlayer == null){
                // Couldn't see a player, so we check if a player is in sensing distance instead
                TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: false);
                range = senseRange;
            }
            return targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.transform.position) < range;
        }
        
        bool TargetClosestPlayerInAnyCase() {
            mostOptimalDistance = 2000f;
            targetPlayer = null;
            for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
            {
                tempDist = Vector3.Distance(transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
                if (tempDist < mostOptimalDistance)
                {
                    mostOptimalDistance = tempDist;
                    targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
                }
            }
            if(targetPlayer == null) return false;
            return true;
        }

        /*/void StickingInFrontOfPlayer() {
            // We only run this method for the host because I'm paranoid about randomness not syncing I guess
            // This is fine because the game does sync the position of the enemy.
            // Also the attack is a ClientRpc so it should always sync
            if (targetPlayer == null || !IsOwner) {
                return;
            }
            if(timeSinceNewRandPos > 0.7f){
                timeSinceNewRandPos = 0;
                if(enemyRandom.Next(0, 5) == 0){
                    // Attack
                }
                else{
                    // Go in front of player
                    positionRandomness = new Vector3(enemyRandom.Next(-2, 2), 0, enemyRandom.Next(-2, 2));
                    StalkPos = targetPlayer.transform.position - Vector3.Scale(new Vector3(-5, 0, -5), targetPlayer.transform.forward) + positionRandomness;
                }
                SetDestinationToPosition(StalkPos, checkForPath: false);
            }
        }/*/

        public override void OnCollideWithPlayer(Collider other) {
            if (timeSinceHittingLocalPlayer < 1f) {
                return;
            }
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
            if (playerControllerB != null)
            {
                LogIfDebugBuild("Example Enemy Collision with Player!");
                timeSinceHittingLocalPlayer = 0f;
                playerControllerB.DamagePlayer(20);
            }
        }

        /*/[ClientRpc]
        public void DoAnimationClientRpc(string animationName) {
            LogIfDebugBuild($"Animation: {animationName}");
            creatureAnimator.SetTrigger(animationName);
        }

        [ClientRpc]
        public void SwingAttackHitClientRpc() {
            LogIfDebugBuild("SwingAttackHitClientRPC");
            int playerLayer = 1 << 3; // This can be found from the game's Asset Ripper output in Unity
            Collider[] hitColliders = Physics.OverlapBox(attackArea.position, attackArea.localScale, Quaternion.identity, playerLayer);
            if(hitColliders.Length > 0){
                foreach (var player in hitColliders){
                    PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(player);
                    if (playerControllerB != null)
                    {
                        LogIfDebugBuild("Swing attack hit player!");
                        timeSinceHittingLocalPlayer = 0f;
                        playerControllerB.DamagePlayer(40);
                    }
                }
            }
        }/*/
    }
}