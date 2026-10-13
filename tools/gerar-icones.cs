// Gera os icones do app em PNG, sem depender de nenhuma biblioteca de imagem.
// Uso: dotnet run tools/gerar-icones.cs
// Desenha um radar simples na cor do topo do app e grava em src/MinhasFinancas.Web/wwwroot.

using System.IO.Compression;

var destino = Path.Combine("src", "MinhasFinancas.Web", "wwwroot");
Directory.CreateDirectory(destino);

foreach (var tamanho in new[] { 180, 512 })
{
    var caminho = Path.Combine(destino, $"icone-{tamanho}.png");
    File.WriteAllBytes(caminho, Png(Desenhar(tamanho), tamanho, tamanho));
    Console.WriteLine($"{caminho} ({tamanho}x{tamanho})");
}

// Renderiza com 4 amostras por eixo e faz a media: e o que deixa as curvas lisas.
static byte[] Desenhar(int tamanho)
{
    const int amostras = 4;
    var pixels = new byte[tamanho * tamanho * 3];

    for (var y = 0; y < tamanho; y++)
    {
        for (var x = 0; x < tamanho; x++)
        {
            int somaR = 0, somaG = 0, somaB = 0;

            for (var sy = 0; sy < amostras; sy++)
            {
                for (var sx = 0; sx < amostras; sx++)
                {
                    var px = x + ((sx + 0.5) / amostras);
                    var py = y + ((sy + 0.5) / amostras);
                    var (r, g, b) = Cor(px, py, tamanho);
                    somaR += r;
                    somaG += g;
                    somaB += b;
                }
            }

            var total = amostras * amostras;
            var i = ((y * tamanho) + x) * 3;
            pixels[i] = (byte)(somaR / total);
            pixels[i + 1] = (byte)(somaG / total);
            pixels[i + 2] = (byte)(somaB / total);
        }
    }

    return pixels;
}

static (int R, int G, int B) Cor(double x, double y, double tamanho)
{
    var fundo = (0x13, 0x4E, 0x4A);
    var anel = (0x2D, 0xD4, 0xBF);
    var varredura = (0x99, 0xF6, 0xE4);
    var branco = (0xFF, 0xFF, 0xFF);

    var centro = tamanho / 2;
    var dx = x - centro;
    var dy = y - centro;
    var distancia = Math.Sqrt((dx * dx) + (dy * dy)) / centro;

    // Direcao da varredura: para cima e para a direita.
    var ux = Math.Cos(-Math.PI / 4);
    var uy = Math.Sin(-Math.PI / 4);

    // O ponto detectado pelo radar.
    var bx = centro + (ux * centro * 0.52);
    var by = centro + (uy * centro * 0.52);
    var raioPonto = tamanho * 0.058;

    if (((x - bx) * (x - bx)) + ((y - by) * (y - by)) <= raioPonto * raioPonto)
    {
        return branco;
    }

    var projecao = (dx * ux) + (dy * uy);
    var perpendicular = Math.Abs((-dx * uy) + (dy * ux));

    if (projecao >= 0 && projecao <= centro * 0.78 && perpendicular <= tamanho * 0.022)
    {
        return varredura;
    }

    foreach (var raio in new[] { 0.38, 0.68 })
    {
        if (Math.Abs(distancia - raio) <= 0.045)
        {
            return anel;
        }
    }

    return fundo;
}

static byte[] Png(byte[] rgb, int largura, int altura)
{
    using var saida = new MemoryStream();
    saida.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    var ihdr = new MemoryStream();
    EscreverInt(ihdr, largura);
    EscreverInt(ihdr, altura);
    ihdr.WriteByte(8);  // bits por canal
    ihdr.WriteByte(2);  // cor RGB
    ihdr.WriteByte(0);  // compressao padrao
    ihdr.WriteByte(0);  // filtro padrao
    ihdr.WriteByte(0);  // sem entrelacamento
    Chunk(saida, "IHDR", ihdr.ToArray());

    // Cada linha e precedida pelo byte de filtro 0.
    var cru = new MemoryStream();
    for (var y = 0; y < altura; y++)
    {
        cru.WriteByte(0);
        cru.Write(rgb, y * largura * 3, largura * 3);
    }

    var comprimido = new MemoryStream();
    using (var zlib = new ZLibStream(comprimido, CompressionLevel.SmallestSize, leaveOpen: true))
    {
        zlib.Write(cru.ToArray());
    }

    Chunk(saida, "IDAT", comprimido.ToArray());
    Chunk(saida, "IEND", []);

    return saida.ToArray();
}

static void Chunk(Stream destino, string tipo, byte[] dados)
{
    EscreverInt(destino, dados.Length);

    var corpo = new byte[4 + dados.Length];
    for (var i = 0; i < 4; i++)
    {
        corpo[i] = (byte)tipo[i];
    }

    dados.CopyTo(corpo, 4);
    destino.Write(corpo);
    EscreverInt(destino, (int)Crc32(corpo));
}

static void EscreverInt(Stream destino, int valor)
{
    destino.WriteByte((byte)(valor >> 24));
    destino.WriteByte((byte)(valor >> 16));
    destino.WriteByte((byte)(valor >> 8));
    destino.WriteByte((byte)valor);
}

static uint Crc32(byte[] dados)
{
    var tabela = new uint[256];

    for (uint i = 0; i < 256; i++)
    {
        var c = i;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }

        tabela[i] = c;
    }

    var crc = 0xFFFFFFFFu;
    foreach (var b in dados)
    {
        crc = tabela[(crc ^ b) & 0xFF] ^ (crc >> 8);
    }

    return crc ^ 0xFFFFFFFFu;
}
