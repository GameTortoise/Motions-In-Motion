using System;

// Pure rules shared by the authoritative trial and the validation suite.
public static class TrialRules
{
    public const float Duration = 1200f;
    public static int Turn(float elapsed) => elapsed < 960f
        ? Math.Min(3, (int)(Math.Max(0f, elapsed) / 240f))
        : Math.Min(7, 4 + (int)((elapsed - 960f) / 60f));
    public static float TurnEnd(int turn) => turn < 4 ? (turn + 1) * 240f : 960f + (turn - 3) * 60f;
    public static PlayerType Team(int turn) => turn % 2 == 0 ? PlayerType.Prosecutor : PlayerType.Defendant;
    public static PlayerType Role(int participant) => participant == 0 ? PlayerType.Judge
        : participant % 2 == 1 ? PlayerType.Prosecutor : PlayerType.Defendant;
    public static bool OwnsEvidence(PlayerType team, int index) => index >= 0 &&
        (team == PlayerType.Prosecutor ? index % 2 == 0 : team == PlayerType.Defendant && index % 2 == 1);
}
