"""Gera texturas estilizadas e repetíveis (seamless) para o jogo. Uso: python3 Tools/art/gen_textures.py <pasta_saida>
Tudo procedural (sem licença de terceiros). Paleta pensada para combinar com os modelos low-poly."""
import sys, os
import numpy as np
from PIL import Image, ImageFilter

N = 512
rng = np.random.default_rng(7)

def tile_noise(scale, octaves=4, persistence=0.5, seed=0):
    """Value noise periódico (sem emenda) somando oitavas."""
    r = np.random.default_rng(seed)
    out = np.zeros((N, N))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = scale * (2 ** o)
        grid = r.random((cells, cells))
        x = np.linspace(0, cells, N, endpoint=False)
        xi = np.floor(x).astype(int); xf = x - xi
        xf = xf * xf * (3 - 2 * xf)
        x0, x1 = xi % cells, (xi + 1) % cells
        a = grid[np.ix_(x0, x0)]; b = grid[np.ix_(x0, x1)]
        c = grid[np.ix_(x1, x0)]; d = grid[np.ix_(x1, x1)]
        fx = xf[None, :]; fy = xf[:, None]
        layer = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
        out += layer * amp; total += amp; amp *= persistence
    return out / total

def colorize(t, stops):
    """t em 0..1 -> cor por gradiente de paradas [(pos,(r,g,b))]."""
    t = np.clip(t, 0, 1)
    img = np.zeros(t.shape + (3,))
    for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
        m = (t >= p0) & (t <= p1)
        k = ((t - p0) / max(p1 - p0, 1e-6))[m][:, None]
        img[m] = np.array(c0) * (1 - k) + np.array(c1) * k
    return img

def save(img, path, blur=0):
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    if blur: im = im.filter(ImageFilter.GaussianBlur(blur))
    im.save(path)
    print('ok', path)

def grass(out):
    n = tile_noise(4, 5, 0.55, seed=1)
    n2 = tile_noise(16, 2, 0.5, seed=2)
    t = 0.65 * n + 0.35 * n2
    img = colorize(t, [(0.0, (74, 128, 52)), (0.45, (98, 160, 64)), (0.7, (122, 182, 76)), (1.0, (150, 196, 92))])
    # tufos: pequenos traços claros e escuros
    for _ in range(2600):
        x, y = rng.integers(0, N, 2)
        l = rng.integers(3, 8)
        shade = rng.choice([1.18, 0.82])
        for k in range(l):
            yy = (y - k) % N; xx = (x + (k // 3)) % N
            img[yy, xx] = img[yy, xx] * shade
    save(img, os.path.join(out, 'grass.png'), blur=0.4)

def soil(out):
    n = tile_noise(8, 4, 0.5, seed=3)
    y = np.arange(N)[:, None] / N
    furrow = 0.5 + 0.5 * np.cos(y * 2 * np.pi * 6)       # 6 sulcos por tile
    t = 0.55 * furrow + 0.45 * n
    img = colorize(t, [(0.0, (70, 44, 28)), (0.4, (104, 68, 42)), (0.75, (132, 90, 56)), (1.0, (152, 108, 68))])
    for _ in range(1800):                                  # pedrinhas
        x, yy = rng.integers(0, N, 2)
        img[yy, x] = img[yy, x] * rng.choice([0.7, 1.25])
    save(img, os.path.join(out, 'soil.png'), blur=0.6)

def path_dirt(out):
    n = tile_noise(6, 5, 0.55, seed=4)
    img = colorize(n, [(0.0, (150, 118, 80)), (0.5, (182, 150, 104)), (1.0, (206, 178, 130))])
    for _ in range(3000):
        x, y = rng.integers(0, N, 2)
        img[y, x] = img[y, x] * rng.choice([0.8, 1.15])
    save(img, os.path.join(out, 'dirt_path.png'), blur=0.5)

def floor_tiles(out):
    img = np.zeros((N, N, 3))
    tiles = 4; s = N // tiles
    n = tile_noise(8, 3, 0.5, seed=5)
    for i in range(tiles):
        for j in range(tiles):
            base = np.array((236, 230, 214)) if (i + j) % 2 == 0 else np.array((214, 206, 188))
            img[i*s:(i+1)*s, j*s:(j+1)*s] = base
    img *= (0.94 + 0.08 * n)[..., None]
    for k in range(tiles):                                 # rejunte
        img[k*s:k*s+3, :] = (150, 140, 126); img[:, k*s:k*s+3] = (150, 140, 126)
    save(img, os.path.join(out, 'floor_tiles.png'), blur=0.4)

if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    grass(out); soil(out); path_dirt(out); floor_tiles(out)
