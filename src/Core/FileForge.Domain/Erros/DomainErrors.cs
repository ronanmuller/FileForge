using FileForge.SharedKernel.Results;

namespace FileForge.Domain.Erros;

/// <summary>Catálogo central de erros de negócio do domínio.</summary>
public static class DomainErrors
{
    public static class Cliente
    {
        public static readonly Error IdVazio = Error.Validation("Cliente.IdVazio", "O identificador do cliente é obrigatório.");
        public static readonly Error NomeObrigatorio = Error.Validation("Cliente.NomeObrigatorio", "O nome do cliente é obrigatório.");
        public static readonly Error ContaDuplicada = Error.Conflict("Cliente.ContaDuplicada", "O cliente já possui uma conta com esta agência e número.");
    }

    public static class Conta
    {
        public static readonly Error AgenciaInvalida = Error.Validation("Conta.AgenciaInvalida", "A agência deve conter apenas dígitos (1 a 5).");
        public static readonly Error NumeroInvalido = Error.Validation("Conta.NumeroInvalido", "O número da conta deve conter apenas dígitos (1 a 12).");
    }

    public static class Transacao
    {
        public static readonly Error ValorZerado = Error.Validation("Transacao.ValorZerado", "O valor da transação deve ser maior que zero.");
        public static readonly Error DescricaoObrigatoria = Error.Validation("Transacao.DescricaoObrigatoria", "A descrição da transação é obrigatória.");
    }

    public static class SolicitacaoArquivo
    {
        public static readonly Error TransicaoInvalida = Error.Conflict("SolicitacaoArquivo.TransicaoInvalida", "Transição de status não permitida.");
        public static readonly Error NomeArquivoObrigatorio = Error.Validation("SolicitacaoArquivo.NomeArquivoObrigatorio", "O nome do arquivo gerado é obrigatório.");
        public static readonly Error MotivoFalhaObrigatorio = Error.Validation("SolicitacaoArquivo.MotivoFalhaObrigatorio", "O motivo da falha é obrigatório.");
        public static readonly Error JaExisteAtiva = Error.Conflict("SolicitacaoArquivo.JaExisteAtiva", "Já existe uma solicitação ativa para este cliente e data.");
    }

    public static class Documento
    {
        public static readonly Error Vazio = Error.Validation("Documento.Vazio", "O documento é obrigatório.");
        public static readonly Error Invalido = Error.Validation("Documento.Invalido", "O documento informado não é um CPF ou CNPJ válido.");
    }

    public static class Dinheiro
    {
        public static readonly Error Negativo = Error.Validation("Dinheiro.Negativo", "O valor monetário não pode ser negativo.");
    }

    public static class DataReferencia
    {
        public static readonly Error NoFuturo = Error.Validation("DataReferencia.NoFuturo", "A data de referência não pode estar no futuro.");
    }
}
