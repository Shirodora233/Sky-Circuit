using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Match
{
    public enum MatchPhase
    {
        Ready,
        Countdown,
        Running,
        Finished
    }

    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] private float countdownDuration = 3f;
        [SerializeField] private float matchDuration = 180f;
        [SerializeField] private BuoyRoute route;
        [SerializeField] private Competitor player;
        [SerializeField] private Competitor opponent;

        private MatchPhase phase = MatchPhase.Ready;
        private float remainingTime;
        private float countdownRemaining;
        private string resultText = "Ready";

        public MatchPhase Phase => phase;
        public float RemainingTime => remainingTime;
        public float CountdownRemaining => countdownRemaining;
        public Competitor Player => player;
        public Competitor Opponent => opponent;
        public string ResultText => resultText;

        public void Configure(BuoyRoute matchRoute, Competitor playerCompetitor, Competitor opponentCompetitor)
        {
            route = matchRoute;
            player = playerCompetitor;
            opponent = opponentCompetitor;
        }

        private void Start()
        {
            BeginCountdown();
        }

        private void Update()
        {
            switch (phase)
            {
                case MatchPhase.Countdown:
                    UpdateCountdown();
                    break;
                case MatchPhase.Running:
                    UpdateRunning();
                    break;
            }
        }

        public void BeginCountdown()
        {
            phase = MatchPhase.Countdown;
            countdownRemaining = countdownDuration;
            remainingTime = matchDuration;
            resultText = "Starting";
            PrepareCompetitorForCountdown(player);
            PrepareCompetitorForCountdown(opponent);
            SetPlayerInputEnabled(false);
            route?.RefreshPlayerTargetVisual(player);
        }

        private void UpdateCountdown()
        {
            countdownRemaining -= Time.deltaTime;
            if (countdownRemaining > 0f)
            {
                return;
            }

            StartMatch();
        }

        private void StartMatch()
        {
            player?.ResetForMatch();
            opponent?.ResetForMatch();
            SetCompetitorFlightActive(player, true);
            SetCompetitorFlightActive(opponent, true);
            SetPlayerInputEnabled(true);
            route?.RefreshPlayerTargetVisual(player);

            countdownRemaining = 0f;
            remainingTime = matchDuration;
            resultText = "Match Running";
            phase = MatchPhase.Running;
        }

        private void UpdateRunning()
        {
            route?.TryScore(player);
            route?.TryScore(opponent);

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                FinishMatch();
            }
        }

        private void FinishMatch()
        {
            remainingTime = 0f;
            phase = MatchPhase.Finished;
            SetPlayerInputEnabled(false);
            SetCompetitorFlightActive(player, false);
            SetCompetitorFlightActive(opponent, false);

            int playerScore = player != null ? player.Score : 0;
            int opponentScore = opponent != null ? opponent.Score : 0;
            if (playerScore > opponentScore)
            {
                resultText = "Player Wins";
            }
            else if (opponentScore > playerScore)
            {
                resultText = "AI Wins";
            }
            else
            {
                resultText = "Draw";
            }
        }

        private static void PrepareCompetitorForCountdown(Competitor competitor)
        {
            competitor?.ResetToSpawn();
            SetCompetitorFlightActive(competitor, false);
        }

        private static void SetCompetitorFlightActive(Competitor competitor, bool active)
        {
            SkyCircuitFlightController controller = competitor != null ? competitor.Controller : null;
            if (controller == null)
            {
                return;
            }

            controller.SetInput(FlightInputState.Neutral);
            controller.enabled = active;

            if (active)
            {
                return;
            }

            var body = controller.GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void SetPlayerInputEnabled(bool enabled)
        {
            PlayerFlightInput input = player != null && player.Controller != null
                ? player.Controller.GetComponent<PlayerFlightInput>()
                : null;
            input?.SetInputEnabled(enabled);
        }
    }
}
