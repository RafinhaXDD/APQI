/**
 * pt-BR is the reference dictionary: its keys define every translatable string.
 * `en.ts` must provide exactly the same keys (enforced by TypeScript).
 * Placeholders use {name}; plural keys end in `.one` / `.other`.
 */
export const ptBR = {
  'app.name': 'AQPI',
  'app.tagline': 'Troque livros com quem está perto de você.',

  'nav.home': 'Início',
  'nav.login': 'Entrar',
  'nav.register': 'Criar conta',
  'nav.profile': 'Meu perfil',
  'nav.logout': 'Sair',
  'nav.language': 'Idioma',
  'nav.main': 'Navegação principal',

  'lang.pt-BR': 'Português',
  'lang.en': 'English',

  'common.loading': 'Carregando…',
  'common.save': 'Salvar',
  'common.saving': 'Salvando…',
  'common.saved': 'Salvo.',
  'common.backHome': 'Voltar ao início',

  'form.email': 'E-mail',
  'form.password': 'Senha',
  'form.displayName': 'Nome de exibição',
  'form.newPassword': 'Nova senha',
  'form.confirmPassword': 'Confirme a nova senha',
  'form.currentPassword': 'Senha atual',
  'form.bio': 'Sobre você',
  'form.emailLanguage': 'Idioma dos e-mails',

  'validation.required': 'Preencha este campo.',
  'validation.email': 'Digite um e-mail válido.',
  'validation.passwordMin': 'A senha precisa ter pelo menos {min} caracteres.',
  'validation.passwordMax': 'A senha pode ter no máximo {max} caracteres.',
  'validation.displayName': 'O nome precisa ter entre {min} e {max} caracteres.',
  'validation.passwordsMatch': 'As senhas não são iguais.',
  'validation.bioMax': 'Use no máximo {max} caracteres.',
  'validation.areaLabel': 'Dê um nome à sua região (até {max} caracteres).',
  'validation.latitude': 'Latitude deve estar entre -90 e 90.',
  'validation.longitude': 'Longitude deve estar entre -180 e 180.',

  'errors.auth.invalid_credentials': 'E-mail ou senha incorretos.',
  'errors.auth.email_not_confirmed': 'Confirme seu e-mail antes de entrar.',
  'errors.auth.session_expired': 'Sua sessão expirou. Entre novamente.',
  'errors.auth.unauthorized': 'Entre para continuar.',
  'errors.auth.invalid_link': 'Este link é inválido ou expirou.',
  'errors.auth.wrong_password': 'A senha atual está incorreta.',
  'errors.rate_limited': 'Muitas tentativas. Aguarde um pouco e tente de novo.',
  'errors.validation.failed': 'Confira os campos destacados.',
  'errors.not_found': 'Não encontramos o que você procurava.',
  'errors.conflict': 'Algo mudou enquanto você editava. Recarregue e tente de novo.',
  'errors.network_error': 'Sem conexão com o servidor. Verifique sua internet.',
  'errors.server_error': 'Algo deu errado do nosso lado. Tente de novo.',

  'home.title': 'Encontre livros perto de você',
  'home.body':
    'Cadastre os livros que você já leu, descubra o que seus vizinhos oferecem e troque pessoalmente.',
  'home.ctaRegister': 'Criar conta grátis',
  'home.ctaLogin': 'Já tenho conta',
  'home.ctaProfile': 'Ir para meu perfil',
  'home.welcome': 'Olá, {name}!',

  'login.title': 'Entrar',
  'login.submit': 'Entrar',
  'login.submitting': 'Entrando…',
  'login.forgot': 'Esqueci minha senha',
  'login.noAccount': 'Ainda não tem conta?',
  'login.resend': 'Reenviar e-mail de confirmação',
  'login.resent': 'Se a conta existir e ainda não estiver confirmada, enviamos um novo e-mail.',

  'register.title': 'Criar conta',
  'register.submit': 'Criar conta',
  'register.submitting': 'Criando…',
  'register.hasAccount': 'Já tem conta?',
  'register.passwordHint': 'Pelo menos {min} caracteres. Uma frase fácil de lembrar funciona bem.',
  'register.checkEmailTitle': 'Confira seu e-mail',
  'register.checkEmailBody':
    'Enviamos um link de confirmação para {email}. Confirme para entrar e ganhar sua primeira ficha.',

  'confirm.title': 'Confirmação de e-mail',
  'confirm.working': 'Confirmando seu e-mail…',
  'confirm.success': 'E-mail confirmado!',
  'confirm.successCredit': 'Você ganhou sua primeira ficha para pedir um livro.',
  'confirm.goLogin': 'Entrar agora',
  'confirm.resendTitle': 'Peça um novo link',
  'confirm.resendSubmit': 'Enviar novo link',

  'forgot.title': 'Esqueci minha senha',
  'forgot.body': 'Digite seu e-mail e enviaremos um link para criar uma nova senha.',
  'forgot.submit': 'Enviar link',
  'forgot.sent':
    'Se existir uma conta com esse e-mail, enviamos um link. Confira sua caixa de entrada.',

  'reset.title': 'Criar nova senha',
  'reset.submit': 'Salvar nova senha',
  'reset.success': 'Senha alterada. Por segurança, saímos de todos os aparelhos.',
  'reset.goLogin': 'Entrar com a nova senha',
  'reset.missingLink': 'Este link está incompleto. Abra o link do e-mail novamente.',

  'profile.title': 'Meu perfil',
  'profile.credits.one': '{count} ficha disponível',
  'profile.credits.other': '{count} fichas disponíveis',
  'profile.creditsHeld': 'Reservadas em trocas: {count}',
  'profile.creditsHelp':
    'Use uma ficha para pedir um livro. Você ganha uma a cada livro que entrega.',
  'profile.detailsTitle': 'Seus dados',
  'profile.homeTitle': 'Sua região',
  'profile.homeBody':
    'Usamos sua localização para mostrar livros perto de você. Ninguém vê sua posição exata, só a distância aproximada e o nome da região.',
  'profile.homeLabel': 'Nome da região (ex.: Campus Norte)',
  'profile.latitude': 'Latitude',
  'profile.longitude': 'Longitude',
  'profile.useLocation': 'Usar minha localização atual',
  'profile.locating': 'Obtendo localização…',
  'profile.locationDenied':
    'Não foi possível obter sua localização. Preencha os campos manualmente.',
  'profile.homeNotSet': 'Você ainda não definiu sua região.',
  'profile.passwordTitle': 'Alterar senha',
  'profile.passwordChanged': 'Senha alterada. Outros aparelhos foram desconectados.',

  'notFound.title': 'Página não encontrada',
  'notFound.body': 'O endereço pode estar errado ou a página foi removida.',
} as const

export type MessageKey = keyof typeof ptBR
export type Messages = Record<MessageKey, string>
