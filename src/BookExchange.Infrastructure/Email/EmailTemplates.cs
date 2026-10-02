using BookExchange.Domain.Users;

namespace BookExchange.Infrastructure.Email;

public sealed record EmailContent(string Subject, string Body);

/// <summary>Plain-text auth emails in the user's preferred language.</summary>
internal static class EmailTemplates
{
    public static EmailContent ConfirmEmail(string language, string name, Uri link) => language == SupportedLanguages.English
        ? new("Confirm your AQPI email", $"Hi {name},\n\nConfirm your email to start exchanging books and receive your first credit:\n{link}\n\nThe link is valid for 24 hours. If you didn't create an account, ignore this email.\n\nAQPI")
        : new("Confirme seu e-mail na AQPI", $"Olá, {name}!\n\nConfirme seu e-mail para começar a trocar livros e receber sua primeira ficha:\n{link}\n\nO link vale por 24 horas. Se você não criou uma conta, ignore este e-mail.\n\nAQPI");

    public static EmailContent PasswordReset(string language, string name, Uri link) => language == SupportedLanguages.English
        ? new("Reset your AQPI password", $"Hi {name},\n\nUse this link to choose a new password:\n{link}\n\nThe link is valid for 24 hours. If you didn't ask for this, ignore this email; your password stays the same.\n\nAQPI")
        : new("Redefina sua senha na AQPI", $"Olá, {name}!\n\nUse este link para escolher uma nova senha:\n{link}\n\nO link vale por 24 horas. Se você não pediu isso, ignore este e-mail; sua senha continua a mesma.\n\nAQPI");

    public static EmailContent AccountExists(string language, string name, Uri loginLink, Uri resetLink) => language == SupportedLanguages.English
        ? new("Someone tried to sign up with your email", $"Hi {name},\n\nSomeone tried to create an AQPI account with this email, but you already have one.\n\nLog in: {loginLink}\nForgot your password? {resetLink}\n\nIf it wasn't you, you can ignore this email.\n\nAQPI")
        : new("Alguém tentou se cadastrar com seu e-mail", $"Olá, {name}!\n\nAlguém tentou criar uma conta na AQPI com este e-mail, mas você já tem uma.\n\nEntrar: {loginLink}\nEsqueceu a senha? {resetLink}\n\nSe não foi você, pode ignorar este e-mail.\n\nAQPI");
}
