namespace AOB.Core.Entities;

public class ConvoyageBirdEntry
{
    public int Id { get; set; }

    public int FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    // 1-based position of this bird within the submission.
    public int BirdOrder { get; set; }

    public int NomenclatureClassId { get; set; }
    public NomenclatureClass NomenclatureClass { get; set; } = null!;

    // Ring number ("anilha") the breeder assigned to the bird.
    public string RingNumber { get; set; } = "";

    // When present, this bird belongs to a team (Equipa) of 4 birds sharing
    // the same EquipaId and having distinct PosicaoEquipa in {"A","B","C","D"}.
    // A/B/C/D reflects the top-to-bottom cage order at the exhibition.
    public Guid? EquipaId { get; set; }
    public string? PosicaoEquipa { get; set; }
}
