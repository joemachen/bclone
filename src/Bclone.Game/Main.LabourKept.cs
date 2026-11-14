using Godot;
using Bclone.Sim.World;

namespace Bclone.Game;

/// <summary>The probe's guard on D745: a professions row built over a village that already has its numbers keeps them.</summary>
public partial class Main
{
    /// <summary>
    /// <c>professions kept:</c> — a row is built for a trade whose number is posed at 3, as a loaded save would hold it, and
    /// the world still says 3 afterwards and the row shows it. Before D745 the row was seeded at 0 and wrote it straight
    /// back, so every load put every trade to 0.
    /// </summary>
    /// <remarks>
    /// ⚠️ The row is built into a throwaway table, never the panel, and everything it adds — the two lists' last entries,
    /// the table — is taken away again, and the trade's number put back as found (D730's trap: leave nothing behind).
    /// </remarks>
    private string AProfessionRowKeepsTheNumberItFinds()
    {
        const JobKind Trade = JobKind.Forager;
        const int Posed = 3;
        int? was = _loop.World.JobLimits.For(Trade);
        int readouts = _professionReadouts.Count, rows = _professionRows.Count;
        _loop.World.JobLimits.Set(Trade, Posed);

        var scratch = new GridContainer { Columns = 3 };
        AddProfessionRow(scratch, Trade);
        int? kept = _loop.World.JobLimits.For(Trade);
        string shown = (_professionRows[^1].Cells[2].GetChild(1) as Label)?.Text ?? "nothing";

        _professionReadouts.RemoveRange(readouts, _professionReadouts.Count - readouts);
        _professionRows.RemoveRange(rows, _professionRows.Count - rows);
        scratch.Free();
        _loop.World.JobLimits.Set(Trade, was);

        return kept == Posed && shown == $"{Posed}"
            ? $"[widths] professions kept: ✅ a row built over a village asking {Posed} on {Trade} keeps {Posed} and shows it (a loaded save keeps its numbers)"
            : $"[widths] professions kept: ⛔ a row built over a village asking {Posed} on {Trade} left it at {kept?.ToString() ?? "unset"} and shows \"{shown}\" — a load would put every trade to 0";
    }
}
