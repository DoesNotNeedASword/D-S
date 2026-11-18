"""Generate the reference props and character meshes into the Unity Resources folder."""
from math import sin, cos, pi
from modeling import Model, DEST, palette
from characters import actor
from equipment import equipment, shipping_crate


def sword():
    m=Model()
    # Grip below the quillons, forged diamond section and a sharply tapering point.
    m.loft("steel",[(0,-.57,0,.027,.024),(0,-.53,0,.086,.061),(0,-.475,0,.079,.060),(0,-.444,0,.038,.037)],8)
    m.loft("leather",[(0,-.46,0,.047,.041),(0,-.42,0,.063,.051),(0,-.18,0,.060,.05),(0,-.15,0,.054,.044)],12)
    for i in range(6):
        m.loft("oak",[(0,-.426+i*.043,0,.062,.054),(0,-.414+i*.043,0,.064,.055)],12,tilt=.009)
    m.plate("iron",[(-.219,-.181,.019),(-.211,-.106,.018),(-.164,-.111,.024),(-.096,-.142,.034),
            (0,-.113,.043),(.096,-.142,.034),(.164,-.111,.024),(.211,-.106,.018),(.219,-.181,.019),
            (.163,-.167,.018),(.089,-.185,.023),(0,-.169,.039),(-.089,-.185,.023),(-.164,-.167,.018)],.062,.011)
    # Each half of the blade reflects from its own plane.
    left=[(-.065,-.127,.001),(-.082,-.078,.001),(-.061,.43,.001),(0,.625,0),
          (0,.423,.036),(0,-.083,.044),(0,-.127,.037)]
    right=[(-x,y,z) for x,y,z in left]
    m.plate("steel",left,.023,.003)
    m.plate("edge",right,.025,.003)
    m.oval("brass",(0,-.151,.055),(.040,.036,.016),8)
    m.save("Longsword")


def shield():
    m=Model()
    profile=[(0,.57),(.30,.4),(.39,.31),(.36,-.115),(.255,-.32),(0,-.57),(-.255,-.32),(-.36,-.115),(-.39,.31),(-.30,.4)]
    outline=[(x,y,.018+.059*(1-abs(x)/.4)) for x,y in profile]
    m.plate("iron",outline,.079,.023)
    board=[(x*.865,y*.884,z+.017) for x,y,z in outline]
    m.plate("grain",board,.022,.016)
    for i in range(-2,3):
        x=i*.122
        def top(x): return .494-.46*abs(x)
        def base(x): return -.489+.93*abs(x)
        def z(x): return .135-.15*abs(x)
        l,r=x-.057,x+.057
        m.plate("honey" if i%2 else "grain",[(l,top(l),z(l)),(r,top(r),z(r)),(r,base(r),z(r)),(l,base(l),z(l))],.027,.004)
    for x,y,z in outline:
        m.oval("steel",(x*.946,y*.943,z+.019),(.020,.020,.012),8,4)
    m.plate("steel",[(-.106,.04,.138),(0,.163,.148),(.106,.04,.138),(0,-.084,.147)],.024,.012)
    # Central peaked boss, four faces.
    a=[m.point(p) for p in [(-.09,.04,.15),(0,-.064,.15),(.09,.04,.15),(0,.142,.15)]]
    apex=m.point((0,.04,.224))
    for i in range(4): m.face([a[i],a[(i+1)%4],apex],"edge" if i%2 else "iron")
    for y in [-.147,.134]:
        m.plate("leather",[(-.175,y-.03,-.075),(.175,y-.03,-.075),(.18,y+.033,-.075),(-.18,y+.033,-.075)],.035,.008)
    m.save("KiteShield")


def bottle():
    m=Model()
    # The liquid is inset at the shoulder. Pale bevels outline a coloured glass body.
    def ring(c,r): m.loft(c,[(0,y,0,x,x) for y,x in r],14)
    ring("glass",[(-.47,.23),(-.441,.291),(-.369,.368),(-.294,.395)])
    ring("violet",[(-.42,.271),(-.355,.353),(-.255,.404),(-.02,.442),(.103,.393),(.151,.35)])
    ring("glass",[(.112,.391),(.155,.365),(.231,.273),(.284,.172),(.316,.149),(.405,.142)])
    ring("gleam",[(.116,.39),(.129,.384)])
    ring("iron",[(.359,.146),(.38,.182),(.445,.183),(.469,.153)])
    ring("steel",[(.419,.183),(.435,.188),(.46,.169)])
    ring("grain",[(.446,.124),(.493,.138),(.561,.147),(.586,.135)])
    ring("honey",[(.567,.147),(.593,.125),(.596,.023)])
    ring("leather",[(.218,.295),(.232,.279),(.258,.23)])
    m.ribbon("leather",[(-.10,-.076,.44),(-.097,.08,.414),(-.059,.215,.298),(-.009,.252,.252)],
             [.047,.051,.049,.042],.015)
    m.plate("brass",[(-.177,-.054,.429),(-.10,.048,.445),(-.019,-.050,.458),(-.095,-.157,.445)],.033,.011)
    m.plate("gem",[(-.139,-.054,.453),(-.10,.005,.461),(-.054,-.051,.467),(-.096,-.113,.459)],.011,.006)
    for i in range(7):
        a=(i*1.71)+.18
        y=-.29+.061*(i%5)
        m.oval("gem",(.423*cos(a),y,.423*sin(a)),(.016,.021,.018),8,4)
    m.save("CorkedFlask")


def herbs():
    m=Model()
    for i in range(11):
        a=i*2.399
        x,z=cos(a),sin(a)
        h=.28+(i%4)*.083
        m.loft("green",[(x*.056,-.437,z*.056,.019,.017),(x*.053,-.17,z*.053,.022,.019),
               (x*.13,.03,z*.13,.018,.015),(x*.244,h,z*.19,.010,.009)],7)
        for j in range(2):
            ht=-.12+j*.15
            m.ribbon("leaf" if i%2 else "lightleaf",[(x*.078,ht,z*.07),(x*.17,ht+.11,z*.15),
                     (x*.27,ht+.216,z*.24),(x*.35,ht+.245,z*.32)],[.014,.067,.051,.002],.022)
        if i%3==0:
            for b in range(7):
                ang=b*2.4
                xx=x*.244+cos(ang)*.032
                zz=z*.19+sin(ang)*.037
                m.loft("lavender" if b%3 else "bud",[(xx,h-.12+b*.033,zz,.013,.012),
                       (xx,h-.08+b*.033,zz,.040,.032),(xx+.015,h-.025+b*.033,zz,.003,.004)],7)
        elif i%3==1:
            for b in range(5):
                ang=b*2*pi/5
                m.oval("ivory",(x*.244+cos(ang)*.037,h+sin(ang)*.028,z*.19+.018),(.03,.026,.02),8,4)
            m.oval("gold",(x*.244,h,z*.19+.038),(.021,.022,.014),8,4)
    for i in range(3):
        m.loft("brass",[(0,-.193+i*.029,0,.093,.089),(0,-.172+i*.029,0,.096,.091)],12,tilt=.012)
    m.ribbon("honey",[(.044,-.161,.064),(.075,-.212,.089),(.129,-.264,.098)],[.02,.025,.014],.012)
    m.save("HerbalBundle")


if __name__=="__main__":
    DEST.mkdir(exist_ok=True, parents=True)
    palette()
    palette("TonicGlass", {"violet":"4c9988", "gem":"8dd0a3"})
    palette("WardGlass", {"violet":"bf7737", "gem":"ebba5f"})
    for asset,kind in [("GoblinRaider","raider"),("CinderShaman","shaman"),("EmberSentinel","sentinel"),
                       ("Apprentice","apprentice"),("CaravanMerchant","merchant")]:
        actor(asset,kind)
    equipment()
    shipping_crate()
    sword()
    shield()
    bottle()
    herbs()

