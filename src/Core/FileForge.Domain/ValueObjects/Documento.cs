using FileForge.Domain.Erros;
using FileForge.SharedKernel.Domain;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.ValueObjects;

public enum TipoDocumento
{
    Cpf,
    Cnpj
}

public sealed class Documento : ValueObject
{
    private Documento(string numero, TipoDocumento tipo)
    {
        Numero = numero;
        Tipo = tipo;
    }

    public string Numero { get; }
    public TipoDocumento Tipo { get; }

    public static Result<Documento> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return DomainErrors.Documento.Vazio;

        var numero = new string(valor.Where(char.IsDigit).ToArray());

        return numero.Length switch
        {
            11 when CpfValido(numero) => new Documento(numero, TipoDocumento.Cpf),
            14 when CnpjValido(numero) => new Documento(numero, TipoDocumento.Cnpj),
            _ => DomainErrors.Documento.Invalido
        };
    }

    private static bool CpfValido(string cpf)
    {
        if (cpf.Distinct().Count() == 1)
            return false;

        return DigitoVerificador(cpf, 9, 10) == cpf[9] - '0'
            && DigitoVerificador(cpf, 10, 11) == cpf[10] - '0';
    }

    private static int DigitoVerificador(string cpf, int tamanho, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < tamanho; i++)
            soma += (cpf[i] - '0') * (pesoInicial - i);

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static bool CnpjValido(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1)
            return false;

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        return DigitoCnpj(cnpj, pesos1) == cnpj[12] - '0'
            && DigitoCnpj(cnpj, pesos2) == cnpj[13] - '0';
    }

    private static int DigitoCnpj(string cnpj, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += (cnpj[i] - '0') * pesos[i];

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Numero;
    }

    public override string ToString() => Numero;
}
