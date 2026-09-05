namespace Infinity.Shell;

public sealed record ScrollModifierKeyRecordingState(bool IsRecording, bool CanSave, bool IsValidationOpen, string ValidationMessage, IReadOnlyList<ScrollModifierKeyLabel> Labels);
