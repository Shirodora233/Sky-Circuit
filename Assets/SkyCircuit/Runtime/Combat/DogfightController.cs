using SkyCircuit.Flight;
using SkyCircuit.Match;
using UnityEngine;

namespace SkyCircuit.Combat
{
    public sealed class DogfightController : MonoBehaviour
    {
        [SerializeField] private MatchController match;
        [SerializeField] private Competitor player;
        [SerializeField] private Competitor opponent;
        [SerializeField] private BackHitFeedback playerFeedback;
        [SerializeField] private BackHitFeedback opponentFeedback;

        [Header("Back Hit")]
        [SerializeField] private float hitDistance = 4.5f;
        [SerializeField] private float behindDotThreshold = -0.55f;
        [SerializeField] private float attackerFacingDotThreshold = 0.35f;
        [SerializeField] private int backHitScore = 2;
        [SerializeField] private float hitCooldown = 1.2f;

        [Header("Repulsion")]
        [SerializeField] private float repulsionVelocityChange = 50f;
        [SerializeField] private float repulsionUpBias = 0.45f;

        private float cooldownRemaining;
        private string lastHitText = "No back hit yet";

        public bool IsUnlocked => match != null && match.DogfightUnlocked;
        public float CooldownRemaining => cooldownRemaining;
        public string LastHitText => lastHitText;

        public void Configure(MatchController matchController, Competitor playerCompetitor, Competitor opponentCompetitor)
        {
            match = matchController;
            player = playerCompetitor;
            opponent = opponentCompetitor;
        }

        public void ConfigureFeedback(BackHitFeedback playerBackFeedback, BackHitFeedback opponentBackFeedback)
        {
            playerFeedback = playerBackFeedback;
            opponentFeedback = opponentBackFeedback;
        }

        private void Update()
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);

            bool active = match != null && match.Phase == MatchPhase.Running;
            bool unlocked = active && IsUnlocked;
            playerFeedback?.SetAvailable(unlocked && cooldownRemaining <= 0f);
            opponentFeedback?.SetAvailable(unlocked && cooldownRemaining <= 0f);

            if (!unlocked || cooldownRemaining > 0f)
            {
                return;
            }

            HitCandidate playerHit = EvaluateHit(player, opponent);
            HitCandidate opponentHit = EvaluateHit(opponent, player);
            if (!playerHit.IsValid && !opponentHit.IsValid)
            {
                return;
            }

            if (playerHit.IsValid && (!opponentHit.IsValid || playerHit.Distance <= opponentHit.Distance))
            {
                ResolveHit(playerHit, opponentFeedback);
            }
            else
            {
                ResolveHit(opponentHit, playerFeedback);
            }
        }

        private HitCandidate EvaluateHit(Competitor attacker, Competitor target)
        {
            if (attacker == null || target == null || attacker.Body == null || target.Body == null)
            {
                return HitCandidate.Invalid;
            }

            Vector3 targetToAttacker = attacker.Body.position - target.Body.position;
            float distance = targetToAttacker.magnitude;
            if (distance <= Mathf.Epsilon || distance > hitDistance)
            {
                return HitCandidate.Invalid;
            }

            Vector3 targetToAttackerDirection = targetToAttacker / distance;
            Vector3 attackerToTargetDirection = -targetToAttackerDirection;
            float behindDot = Vector3.Dot(target.Body.forward, targetToAttackerDirection);
            if (behindDot > behindDotThreshold)
            {
                return HitCandidate.Invalid;
            }

            float attackerFacingDot = Vector3.Dot(attacker.Body.forward, attackerToTargetDirection);
            if (attackerFacingDot < attackerFacingDotThreshold)
            {
                return HitCandidate.Invalid;
            }

            return new HitCandidate(attacker, target, distance, targetToAttackerDirection);
        }

        private void ResolveHit(HitCandidate hit, BackHitFeedback targetFeedback)
        {
            hit.Attacker.AddBackHitScore(backHitScore);
            cooldownRemaining = hitCooldown;
            lastHitText = $"{hit.Attacker.DisplayName} back hit {hit.Target.DisplayName} +{backHitScore}";
            targetFeedback?.TriggerHit();
            ApplyRepulsion(hit);
        }

        private void ApplyRepulsion(HitCandidate hit)
        {
            Vector3 separation = hit.TargetToAttackerDirection;
            Vector3 attackerVelocity = (separation + Vector3.up * repulsionUpBias).normalized * repulsionVelocityChange;
            Vector3 targetVelocity = (-separation + Vector3.up * repulsionUpBias).normalized * repulsionVelocityChange;
            hit.Attacker.Controller?.ApplyExternalImpulse(attackerVelocity);
            hit.Target.Controller?.ApplyExternalImpulse(targetVelocity);
        }

        private readonly struct HitCandidate
        {
            public static readonly HitCandidate Invalid = new HitCandidate(null, null, 0f, Vector3.zero);

            public readonly Competitor Attacker;
            public readonly Competitor Target;
            public readonly float Distance;
            public readonly Vector3 TargetToAttackerDirection;

            public bool IsValid => Attacker != null && Target != null;

            public HitCandidate(Competitor attacker, Competitor target, float distance, Vector3 targetToAttackerDirection)
            {
                Attacker = attacker;
                Target = target;
                Distance = distance;
                TargetToAttackerDirection = targetToAttackerDirection;
            }
        }
    }
}
