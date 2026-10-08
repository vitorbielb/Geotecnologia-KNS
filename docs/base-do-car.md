# Base do CAR

O perímetro de todo imóvel analisado vem daqui. Sem esta base o sistema não sabe
onde fica a fazenda, e nenhuma das onze regras tem contra o que cruzar.

## De onde vem: o WFS público do SICAR

**É de graça, é oficial, e não precisa de cadastro.** O SICAR publica a base em
`https://geoserver.car.gov.br/geoserver/sicar/wfs` — uma camada por unidade da
federação, `sicar:sicar_imoveis_XX`, com código do CAR, perímetro, município,
área e condição do cadastro.

> Vale registrar porque a conclusão anterior estava errada e teria custado
> dinheiro: parecia que o download em massa exigia compra ou passava pelo
> CAPTCHA da consulta pública. Os dois portais de download do `car.gov.br` caem
> mesmo na consulta protegida, e o GeoServer em `/geoserver/ows` responde 200
> com a lista de camadas **vazia**. O endereço acima é outro, é documentado, e
> serve a base inteira.

## Como carregar

```bash
# os municípios que alguém consultou e não estavam carregados
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- baixar-car --lacunas

# um município
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- baixar-car --municipio 5107925

# um estado inteiro
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- baixar-car --uf MT
```

Na prática quase nunca é preciso rodar: a aplicação atende as lacunas sozinha,
de hora em hora. Cadastrar um imóvel de um município que o sistema nunca viu
falha uma vez e passa a funcionar na hora seguinte, sem ninguém ser acionado.
Para desligar, `Camadas:PreencherBaseCar = false`.

## Por município, e não o país inteiro

A granularidade é escolha de produto. Um município de fornecedor sai em
segundos; o país inteiro são milhões de imóveis que ninguém pediu.

O sistema já registrava a demanda: quando uma consulta não encontra o imóvel,
ele anota o município como **lacuna**. Agora a lacuna é o pedido de carga.

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- cobertura            # resumo por UF
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- cobertura --uf GO    # município a município
```

O `--uf` é como se confere se um estado está coberto de verdade: município com
meia dúzia de imóveis costuma ser carona na camada do vizinho, não carga.

Referência das cargas feitas:

| Estado | Municípios | Imóveis | Tempo |
|---|---|---|---|
| Pará | 144 | 390.587 | 633s |
| Goiás | 246 | 243.921 | 585s |
| Mato Grosso | 142 | 219.983 | 391s |
| Rondônia | 52 | 196.929 | 239s |
| Tocantins | 139 | 112.081 | 190s |
| Mato Grosso do Sul | 79 | 90.086 | 157s |

Cerca de 1,25 milhão de imóveis no cinturão da pecuária, pouco mais de 35
minutos de carga.

Confira a contagem de municípios contra o IBGE **do ano corrente**, não contra o
que você lembra: Mato Grosso aparece com 142 e não com 141 porque Boa Esperança
do Norte foi desmembrada de Nova Mutum. Município novo entra na base com poucos
imóveis — os cadastros antigos continuam sob o código do município de origem até
serem retificados.

## Paginação: o teto de dez mil

O servidor devolve no máximo **10.000 feições por requisição** — inclusive na
contagem (`resultType=hits`), que também sai limitada em dez mil e por isso
**não serve** para saber o tamanho real de um estado.

A baixa pagina com `startIndex`, e ordena por `cod_imovel`. A ordenação não é
estética: sem ordem explícita, duas páginas podem repetir um imóvel e pular
outro, e o buraco não apareceria em lugar nenhum. O fim é detectado por página
incompleta.

Referência: uma página cheia (10 mil imóveis, 5,6 MB) leva cerca de 6 segundos.

## A armadilha mais cara: a versão do WFS

**Use `version=1.0.0`.** A partir da 1.1.0 o servidor honra a ordem de eixos
declarada no EPSG:4674 — latitude antes de longitude — e o SHAPE-ZIP sai com as
duas **trocadas**. O GeoJSON vem certo nas duas versões, o que torna o defeito
ainda mais fácil de não ver.

Isso já aconteceu aqui, com 1,25 milhão de imóveis. Abaetetuba ficou em
longitude −1,76, que é no Atlântico perto da África. E **nada falhou**: o
cruzamento simplesmente não encontrava nada, e toda análise devolvia "nenhuma
sobreposição". Laudo limpo para todo fornecedor é o pior resultado possível, e é
o que mais se parece com sucesso.

Só apareceu porque São Félix do Xingu, epicentro de desmatamento, saiu com zero
sobreposições em oito imóveis seguidos. Depois da correção, 11 de 15.

Três defesas ficaram no lugar:

- `inspecionar` mostra a extensão da amostra e avisa quando ela cai fora do
  Brasil. **Rode antes de carregar arquivo de origem nova** — é o comando que
  existe para isso.
- O importador descarta imóvel fora do Brasil e **recusa o arquivo inteiro**
  quando mais de 10% dele cai fora. Imóvel solto é dado ruim na origem; arquivo
  inteiro é o arquivo errado.
- A caixa de plausibilidade não é o contorno do país, e não precisa ser: eixo
  trocado erra por dezenas de graus, não por meio. Buenos Aires cai dentro dela
  de propósito — apertar até excluir o vizinho recusaria imóvel legítimo de
  fronteira.

## Armadilhas que custaram tempo

**O `cod_municipio_ibge` e o código do CAR discordam.** Existem imóveis cujo
atributo diz um município e cujo código do CAR embute outro. Uma carga filtrada
por município traz esses junto.

Eles **são gravados** — são dados reais, e descartá-los seria pior. Mas o
município deles **não é dado por coberto**: se fosse, uma consulta futura lá não
registraria lacuna e ninguém descobriria que falta carregar. Cobertura afirma
"este município foi carregado por inteiro", e essa afirmação precisa ser
verdadeira.

O mesmo vale por estado: uma carga de Goiás trouxe dois imóveis com código do
Distrito Federal e um com código de Minas. Gravados, não cobertos — dois imóveis
registrando o Distrito Federal como coberto seria a forma mais barata de
esconder um estado inteiro.

Se uma carga antiga registrou cobertura que não cobriu:

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- cobertura --remover 1720259
```

**A contagem por município sai do banco, não do arquivo.** Uma carga por páginas
chama o registro de cobertura uma vez por página, e município grande aparece em
várias: contar o que veio na página fazia a última sobrescrever as anteriores.
Rio Verde aparecia com 5.766 imóveis quando tinha 6.295. Para corrigir cargas
feitas antes disso:

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- cobertura --recontar
```

**Cadastro cancelado entra na base.** Cerca de **6%** dos registros são
"Cancelado por decisão administrativa". Eles são carregados com a condição
registrada, não descartados — senão a análise responderia "imóvel não está na
base do CAR" para um imóvel que está lá e teve o cadastro anulado. São duas
situações diferentes, e o laudo não pode confundi-las.

**A condição vem por extenso.** "Analisado, aguardando regularização ambiental
(Lei nº 12.651/2012)" tem 68 caracteres; a coluna era `varchar(50)` e a carga
morria. Hoje são 120, e o importador corta no tamanho da coluna como defesa
contra uma origem estadual mais verbosa.

## Campos que o importador reconhece

Confira antes de carregar um arquivo de outra origem:

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- inspecionar --arquivo <arquivo.shp>
```

| Destino | Nomes aceitos |
|---|---|
| `codigo_car` | COD_IMOVEL, CAR, COD_CAR, CODIGO_CAR, CAR_ID |
| `area_ha` | NUM_AREA, AREA_HA, AREA, AREA_IMOVE |
| `municipio` | MUNICIPIO, NOM_MUNICI, NM_MUNICIP, NOME_MUNIC |
| `uf` | COD_ESTADO, UF, SIGLA_UF, ESTADO |
| `situacao` | CONDICAO, DES_CONDIC, IND_STATUS, SITUACAO, STATUS, STATUS_IMO, STATUS_IMOVEL |
| `tipo` | IND_TIPO, TIPO_IMOVE, TIPO_IMOVEL, TIPO |

O código do IBGE e a UF saem do próprio código do CAR, que os embute — não
dependem de coluna.

## Recarga

A carga é **upsert por código do CAR**: rodar de novo atualiza o que mudou e
acrescenta o que é novo, sem apagar nada. Recarregar um estado é rotina segura.

A base do CAR muda todo dia, mas devagar e por acréscimo. Não há recarga
automática periódica como nas camadas de referência — se ela passar a fazer
falta, o comando por UF já existe e pode ser agendado.

## A situação do cadastro, e por que ela não é embargo

A regra **CAR-001** examina a validade do cadastro. Ela é separada da regra de
embargo de propósito, e a distinção decide o que o laudo pode afirmar.

**Embargo é sanção.** Vem de auto de infração, recai sobre área determinada e
proíbe atividade econômica ali. Comprar de área embargada gera responsabilidade
direta — por isso ele bloqueia.

**Cancelamento de CAR é anulação de registro.** Diz que aquele cadastro não
vale, não que a área está sob sanção. Pode não haver infração nenhuma.

O que o cancelamento é, de fato, incomoda de outro jeito: todas as regras
geográficas são calculadas contra o perímetro do CAR. Com o cadastro anulado, o
cruzamento rodou sobre um polígono que o Estado não reconhece mais como a
declaração daquele imóvel. A geometria não está errada — perdeu o lastro.

### Nem todo cancelamento significa a mesma coisa

O SICAR cancela cadastro por cinco motivos, e três deles são rotina de
cartório. Na base carregada:

| Motivo | Imóveis | Dispara |
|---|---|---|
| por decisão administrativa | 77.935 | sim |
| por decisão judicial | 2.965 | sim |
| por solicitação do proprietário | 530 | não |
| por alteração na base de municípios | 21 | não |
| por duplicidade | 1 | não |

A divisa do município mudou, o cadastro estava duplicado, o dono pediu para
recadastrar: nenhum desses é restrição ambiental. Uma regra única sobre
"Cancelado" colocaria redesenho de divisa municipal no mesmo balde que anulação
judicial — e **afirmar restrição onde não há é o mesmo erro que liberar onde há,
invertido**.

### Severidade

Alerta no protocolo padrão, não bloqueio: o critério é que bloqueio fica para
área sob sanção. Se a indústria exigir CAR ativo por obrigação contratual — os
acordos de carne costumam exigir —, o corte é dela e a regra aceita bloqueio;
muda a severidade e o fundamento citado no laudo, que passa a ser contratual em
vez de ambiental.

Situação **não informada** sai como regra não avaliada, nunca como cadastro em
ordem. É o mesmo cuidado da cadeia indireta: um imóvel que ninguém conferiu não
é um imóvel conferido.

### O que ainda não é olhado

Dois conjuntos grandes que o sistema hoje ignora, e que talvez pesem mais que um
cancelamento por duplicidade:

- **399.041 imóveis (31,8%)** em "Analisado, aguardando atendimento a
  notificação" — o órgão examinou e notificou.
- **50.585 (4,0%)** em "Analisado, aguardando regularização ambiental".

Nenhum dos dois é sanção, mas ambos são sinal de que o órgão achou pendência.
Fica anotado.
