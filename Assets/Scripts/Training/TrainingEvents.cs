// Avisos de las mecanicas para el entrenamiento guiado ("disparo", "recarga",
// "blanco:galeria", "martillo_vuelve"...). Si no hay entrenamiento, nadie escucha.
public static class TrainingEvents
{
    public static event System.Action<string> Raised;

    public static void Raise(string id)
    {
        Raised?.Invoke(id);
    }
}
