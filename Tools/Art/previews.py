"""Render orthographic inventory icons and an art sheet from the editable OBJ meshes."""
from math import radians, sin, cos
from PIL import Image, ImageDraw, ImageFont
import numpy as np
from modeling import DEST, PALETTE


def render(name, size, yaw=-18, elevation=8, overrides=None):
    vertices,faces=[],[]
    colors=list(PALETTE)
    for line in (DEST/(name+".obj")).read_text().splitlines():
        row=line.split()
        if not row: continue
        if row[0]=="v": vertices.append([float(v) for v in row[1:4]])
        if row[0]=="f":
            indices=[part.split("/") for part in row[1:]]
            faces.append(([int(i[0])-1 for i in indices],colors[int(indices[0][1])-1]))
    a,b=radians(yaw),radians(elevation)
    turn=np.array([[cos(a),0,sin(a)],[0,1,0],[-sin(a),0,cos(a)]])
    pitch=np.array([[1,0,0],[0,cos(b),-sin(b)],[0,sin(b),cos(b)]])
    v=np.asarray(vertices) @ turn.T @ pitch.T
    points=v[:,:2].copy()
    points[:,1]*=-1
    low,high=points.min(axis=0),points.max(axis=0)
    scale=min(size[0]/(high[0]-low[0]),size[1]/(high[1]-low[1]))*.92
    points=(points-(low+high)/2)*scale+(np.array(size)/2)
    pixels=np.zeros((size[1],size[0],4),dtype=np.uint8)
    depth=np.full((size[1],size[0]),-1e9)
    light=np.array([-.42,.67,.61])
    for indices,key in faces:
        q=v[indices]
        normal=np.cross(q[1]-q[0],q[2]-q[0])
        normal/=max(np.linalg.norm(normal),1e-6)
        if normal[2]<-.001: continue
        diffuse=max(0,np.dot(normal,light))
        base=(overrides or {}).get(key,PALETTE[key])
        rgb=np.array(list(bytes.fromhex(base)), dtype=float)
        tone=rgb*(.70+.36*diffuse)+np.array([4,3,7])
        color=tuple(np.clip(tone,0,255).astype(int))+(255,)
        for k in range(1,len(indices)-1):
            ids=[indices[0],indices[k],indices[k+1]]
            tri=points[ids]
            lo=np.maximum(np.floor(tri.min(axis=0)).astype(int),0)
            hi=np.minimum(np.ceil(tri.max(axis=0)).astype(int),np.asarray(size)-1)
            if (lo>hi).any(): continue
            (x0,y0),(x1,y1),(x2,y2)=tri
            denom=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2)
            if abs(denom)<1e-7: continue
            y,x=np.ogrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
            u=((y1-y2)*(x+.5-x2)+(x2-x1)*(y+.5-y2))/denom
            w=((y2-y0)*(x+.5-x2)+(x0-x2)*(y+.5-y2))/denom
            t=1-u-w
            z=u*v[ids[0],2]+w*v[ids[1],2]+t*v[ids[2],2]
            area=np.s_[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
            visible=(u>=0)&(w>=0)&(t>=0)&(z>depth[area])
            depth[area][visible]=z[visible]
            pixels[area][visible]=color
    return Image.fromarray(pixels)


def icons():
    folder=DEST.parent/"InventoryArt"
    folder.mkdir(exist_ok=True)
    items=["CorkedFlask","HerbalBundle","Longsword","KiteShield","FieldCoat","SpringTrap","Longsword",
           "IronOre","CoalHeart","CorkedFlask","Boots","Breeches","VisoredHelm","CorkedFlask","CorkedFlask"]
    for i,name in enumerate(items):
        swap={0:{"violet":"4c9988","gem":"8dd0a3"},13:{"violet":"bf7737","gem":"ebba5f"}}.get(i)
        im=render(name,(300,300),elevation=28 if i==5 else 5,overrides=swap)
        cropped=im.crop(im.getbbox())
        cropped.thumbnail((142,142),Image.Resampling.LANCZOS)
        cropped.save(folder/(str(i)+".png"))


def gallery():
    image=Image.new("RGB",(1600,900),"#e8ddc8")
    draw=ImageDraw.Draw(image)
    def font(size):
        try: return ImageFont.truetype("georgia.ttf",size)
        except OSError: return ImageFont.load_default(size=size)
    draw.text((45,27),"Лавка и подземелье",fill="#3a2c29",font=font(42))
    draw.line((45,90,1555,90),fill="#ac9170",width=2)
    actors=[("GoblinRaider","Налётчик"),("CinderShaman","Шаман"),("EmberSentinel","Огненный страж"),
            ("Apprentice","Помощница"),("CaravanMerchant","Караванщик")]
    for i,(name,label) in enumerate(actors):
        cx=162+i*319
        tile=render(name,(480,570),yaw=-23,elevation=4).resize((288,342),Image.Resampling.LANCZOS)
        draw.ellipse((cx-68,437,cx+68,450),fill="#c6b79e")
        image.paste(tile,(cx-144,109),tile)
        draw.text((cx,471),label,fill="#45352e",font=font(23),anchor="mt")
    draw.line((45,528,1555,528),fill="#ac9170")
    props=[("Longsword","Клинок"),("KiteShield","Щит"),("HerbalBundle","Сбор трав"),("CorkedFlask","Алхимическая смесь")]
    for i,(name,label) in enumerate(props):
        cx=208+i*395
        tile=render(name,(390,420)).resize((227,245),Image.Resampling.LANCZOS)
        image.paste(tile,(cx-114,554),tile)
        draw.text((cx,824),label,fill="#45352e",font=font(23),anchor="mt")
    image.save(DEST.parents[3]/"Docs"/"ModelSheet.png",optimize=True)


if __name__=="__main__":
    icons()
    gallery()

