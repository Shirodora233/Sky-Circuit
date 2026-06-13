using SkyCircuit.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SkyCircuit.Profiles
{
    [RequireComponent(typeof(Competitor))]
    public sealed class PlayerProfileSwitcher : MonoBehaviour
    {
        [SerializeField] private Competitor competitor;
        [SerializeField] private CompetitorProfile speederProfile;
        [SerializeField] private CompetitorProfile fighterProfile;
        [SerializeField] private CompetitorProfile allRounderProfile;
        [SerializeField] private bool resetSpeedOnSwitch = false;

        public void Configure(
            Competitor targetCompetitor,
            CompetitorProfile speeder,
            CompetitorProfile fighter,
            CompetitorProfile allRounder)
        {
            competitor = targetCompetitor;
            speederProfile = speeder;
            fighterProfile = fighter;
            allRounderProfile = allRounder;
        }

        private void Awake()
        {
            if (competitor == null)
            {
                competitor = GetComponent<Competitor>();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || competitor == null)
            {
                return;
            }

            if (WasPressed(keyboard.digit1Key, keyboard.numpad1Key))
            {
                SwitchTo(speederProfile);
            }
            else if (WasPressed(keyboard.digit2Key, keyboard.numpad2Key))
            {
                SwitchTo(fighterProfile);
            }
            else if (WasPressed(keyboard.digit3Key, keyboard.numpad3Key))
            {
                SwitchTo(allRounderProfile);
            }
        }

        private void SwitchTo(CompetitorProfile profile)
        {
            competitor.SetProfile(profile, resetSpeedOnSwitch);
        }

        private static bool WasPressed(KeyControl topRowKey, KeyControl numpadKey)
        {
            return (topRowKey != null && topRowKey.wasPressedThisFrame)
                || (numpadKey != null && numpadKey.wasPressedThisFrame);
        }
    }
}
