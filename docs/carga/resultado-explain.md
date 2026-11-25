# Teste de carga: ListarPorContasEDataAsync

Data: 2026-11-25
Banco: PostgreSQL 17.12 (container `fileforge-pg`, Docker Desktop)
Teste: `tests/FileForge.IntegrationTests/Carga/TransacaoCargaTests.cs`
Execução: `dotnet test tests/FileForge.IntegrationTests --filter "Categoria=Carga"`

## Massa de dados

- 10 contas, todas do mesmo cliente
- 100.000 transações por conta (1.000.000 no total)
- Datas distribuídas em 30 dias a partir de 2025-01-10
- Valores e tipos alternados (crédito e débito)

## Consulta executada

```sql
SELECT t.* FROM transacoes t
WHERE t.conta_id = ANY(@ids) AND t.data = @data
ORDER BY t.data, t.id
```

## Resultado (EXPLAIN ANALYZE, BUFFERS)

| Item | Valor |
|---|---|
| Linhas retornadas | 33.340 |
| Plano | Sort → Bitmap Heap Scan → Bitmap Index Scan em `ix_transacoes_conta_id_data` |
| Execution Time | 45,6 ms |
| Planning Time | 0,7 ms |
| Buffers | 9.425 em cache, 1.416 lidos do disco |
| Sort | em memória (quicksort, 3.550 kB) |

## Leitura pelo repositório

| Item | Valor |
|---|---|
| Transações lidas pelo `ListarPorContasEDataAsync` | 33.340 |
| Tempo total (EF + hidratação + streaming) | 772 ms |

## Conclusões

1. O planner usa o índice `ix_transacoes_conta_id_data`. Não há `Seq Scan`.
2. O custo do banco é baixo (45 ms). A maior parte do tempo do repositório está na materialização de 33 mil entidades pelo EF, não no SQL.
3. O `Sort` aparece porque o índice é `(conta_id, data)` e o `ORDER BY` pede `data, id`. Um índice `(conta_id, data, id)` eliminaria esse passo. Não foi criado ainda: depende de medir o ganho.

## Limitações

- Dados sintéticos e uniformes. Dados reais podem mudar o plano.
- Uma execução apenas. Para decisão de índice, repetir pelo menos 5 vezes.
- Teste de cancelamento (`CancelamentoInterrompeALeitura`) também passou, confirmando que o token chega até o banco.
