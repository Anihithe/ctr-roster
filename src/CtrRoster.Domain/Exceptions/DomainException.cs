namespace CtrRoster.Domain.Exceptions;

/// <summary>
/// Exception métier levée lors de la violation d'une règle de gestion du domaine.
/// Le message de cette exception est destiné à être retourné à l'utilisateur de manière conviviale.
/// </summary>
public class DomainException(string message) : Exception(message);
