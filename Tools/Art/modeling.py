"""Small offline polygon-modeling helpers for the shop's painted OBJ models."""
from math import cos, sin, pi, sqrt
from pathlib import Path
import struct
import zlib

DEST = Path(__file__).resolve().parents[2] / "Assets/Prototype/Resources/Atelier"
PALETTE = {
    "skin": "d99462", "blush": "b67250", "goblin": "94a349", "cheek": "73853b",
    "ear": "b58754", "lip": "664634", "ivory": "f6deae", "eye": "edc65b",
    "ink": "312932", "hair": "493137", "hairlight": "71473e", "beard": "9c7253",
    "linen": "ddc39b", "linenlight": "f6dfae", "olive": "727744", "shadow": "515032",
    "wine": "833c3a", "red": "ad5245", "plum": "50334e", "plumlight": "785171",
    "leather": "784b37", "oak": "50392f", "grain": "926144", "honey": "ad7950",
    "brass": "c59055", "gold": "edba71", "iron": "69616a", "steel": "a99894",
    "edge": "d8c9ba", "obsidian": "40323b", "basalt": "604049", "crust": "8a4c43",
    "lava": "ff7728", "fire": "ffb539", "sun": "ffe07b", "violet": "a44adc",
    "gem": "cf7afa", "glass": "b1c7ba", "gleam": "e1e5d4", "leaf": "899639",
    "green": "586e30", "lightleaf": "b1b749", "lavender": "9b6fa2", "bud": "765384",
    "tonic": "56a79a", "amber": "d19136",
}


def palette(name='Atelier', overrides=None):
    # Four texels of padding keep nearest and bilinear sampling on the same swatch.
    colors = [((overrides or {}).get(key, value)) for key, value in PALETTE.items()]
    w, h = 64, 48
    rows = []
    for y in range(h):
        row = bytearray()
        for x in range(w):
            index = y // 8 * 8 + x // 8
            row.extend(bytes.fromhex(colors[index] if index < len(colors) else colors[0]))
        rows.append(b"\0" + row)
    def chunk(name, data):
        return struct.pack("!I", len(data)) + name + data + struct.pack("!I", zlib.crc32(name + data))
    image = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack("!2I5B", w, h, 8, 2, 0, 0, 0))
    (DEST / (name+".png")).write_bytes(image + chunk(b"IDAT", zlib.compress(b"".join(rows))) + chunk(b"IEND", b""))
    (DEST / "Paint.mtl").write_text("newmtl Paint\nKd 1 1 1\nmap_Kd Atelier.png\n", encoding="ascii")


class Model:
    def __init__(self):
        self.vertices = []
        self.faces = {}
        self.part = "Body"

    def point(self, p):
        self.vertices.append(p)
        return len(self.vertices)

    def face(self, points, color):
        self.faces.setdefault(self.part, []).append((points, color))

    def loft(self, color, rings, sides=12, pleat=0, tilt=0):
        """Closed elliptical sections (x, y, z, radius x, radius z), bottom to top."""
        rings_out = []
        for i, (x, y, z, rx, rz) in enumerate(rings):
            row = []
            for k in range(sides):
                a = 2*pi*k/sides + pi/sides
                fold = 1 + pleat * (-1 if k % 2 else 1)
                row.append(self.point((x+rx*cos(a)*fold, y+tilt*cos(a), z+rz*sin(a)*fold)))
            rings_out.append(row)
        self.face(rings_out[0], color)
        self.face(rings_out[-1][::-1], color)
        for lower, upper in zip(rings_out, rings_out[1:]):
            for j in range(sides):
                k = (j+1) % sides
                self.face([lower[j], upper[j], upper[k], lower[k]], color)

    def oval(self, color, center, radii, sides=12, bands=7, slope=0):
        x,y,z = center
        rx,ry,rz = radii
        rings = []
        for i in range(bands+1):
            a = -pi/2 + pi*i/bands
            rings.append((x, y+ry*sin(a), z+slope*sin(a), rx*max(.035,cos(a)), rz*max(.035,cos(a))))
        self.loft(color, rings, sides)

    def band(self, color, x, y, z, rx, rz, h, sides=14):
        self.loft(color, [(x,y-h/2,z,rx*.91,rz*.91), (x,y-h*.32,z,rx,rz),
                          (x,y+h*.32,z,rx,rz), (x,y+h/2,z,rx*.91,rz*.91)], sides)

    def plate(self, color, outline, depth=.026, bevel=.015):
        """Convex outline in the XY plane with a raised, inset face and bevelled rim."""
        cx = sum(p[0] for p in outline)/len(outline)
        cy = sum(p[1] for p in outline)/len(outline)
        area = sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(outline, outline[1:]+outline[:1]))
        if area < 0: outline = outline[::-1]
        back, edge, face = [], [], []
        for x,y,z in outline:
            back.append(self.point((x,y,z-depth)))
            edge.append(self.point((x,y,z)))
            d = max(bevel, sqrt((x-cx)**2+(y-cy)**2), .001)
            face.append(self.point((x+(cx-x)*bevel/d, y+(cy-y)*bevel/d, z+bevel*.6)))
        self.face(back[::-1], color)
        self.face(face,color)
        for i in range(len(edge)):
            j=(i+1)%len(edge)
            self.face([back[i],back[j],edge[j],edge[i]],color)
            self.face([edge[i],edge[j],face[j],face[i]],color)

    def ribbon(self, color, points, widths, depth=.025):
        # A shaped strip with a ridge. Can sweep through three dimensions.
        strips = []
        for (x,y,z),w in zip(points,widths):
            strips.append([self.point((x-w,y,z)), self.point((x,y,z+depth)), self.point((x+w,y,z)),
                           self.point((x,y,z-depth))])
        for a,b in zip(strips,strips[1:]):
            for i in range(4):
                j=(i+1)%4
                self.face([a[j],b[j],b[i],a[i]],color)
        self.face(strips[0],color)
        self.face(strips[-1][::-1],color)

    def save(self, name):
        colors = list(PALETTE)
        lines=["mtllib Paint.mtl", "s off"]
        for x,y,z in self.vertices:
            lines.append(f"v {x:.5f} {y:.5f} {z:.5f}")
        for i in range(len(colors)):
            lines.append(f"vt {(i%8+.5)/8:.6f} {1-(i//8+.5)/6:.6f}")
        norm_id=0
        for part,faces in self.faces.items():
            lines += [f"g {part}", "usemtl Paint"]
            for indices,color in faces:
                t=colors.index(color)+1
                # Explicit face normals keep bevels, folds and crystal planes faceted in every importer.
                normal=[0.,0.,0.]
                for a,b in zip(indices, indices[1:]+indices[:1]):
                    x,y,z=self.vertices[a-1]
                    u,v,w=self.vertices[b-1]
                    normal[0]+=(y-v)*(z+w)
                    normal[1]+=(z-w)*(x+u)
                    normal[2]+=(x-u)*(y+v)
                length=sqrt(sum(n*n for n in normal))
                if length<1e-9: continue
                norm_id+=1
                lines.append("vn "+" ".join(f"{n/length:.6f}" for n in normal))
                lines.append("f "+" ".join(f"{v}/{t}/{norm_id}" for v in indices))
        (DEST / (name+".obj")).write_text("\n".join(lines)+"\n",encoding="ascii")

