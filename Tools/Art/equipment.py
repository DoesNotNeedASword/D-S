from math import pi, sin, cos
from modeling import Model


def equipment():
    for kind in ("Boots","Breeches","FieldCoat","VisoredHelm","IronOre","CoalHeart","SpringTrap"):
        m=Model()
        if kind in ("Boots","Breeches"):
            for s in (-1,1):
                x=s*.193
                if kind=="Boots":
                    m.loft("oak",[(x,-.37,.06,.168,.27),(x,-.32,.06,.176,.279),(x,-.27,.068,.174,.267)],14)
                    m.loft("leather",[(x,-.28,.068,.172,.262),(x,-.205,.082,.166,.256),
                          (x,-.109,.018,.143,.181),(x,.006,-.032,.13,.146),(x,.186,-.043,.15,.158)],14)
                    m.band("grain",x,.172,-.043,.168,.168,.099)
                    for j in range(3):
                        m.ribbon("brass",[(x-.062,-.117+j*.046,.173),(x+.059,-.092+j*.044,.169)],[.015,.014],.008)
                else:
                    m.loft("shadow",[(x,-.45,0,.096,.096),(x,-.40,0,.11,.12),(x,-.27,.006,.119,.14),
                       (x,-.14,.019,.139,.146),(x,-.05,0,.153,.16),(x,.19,0,.19,.183)],14,.025)
                    m.band("grain",x,-.39,0,.113,.128,.055)
            if kind=="Breeches":
                m.loft("olive",[(0,.134,0,.371,.176),(0,.276,0,.343,.184),(0,.333,0,.293,.168)],16)
                m.band("oak",0,.295,0,.332,.189,.065)
                m.plate("brass",[(-.055,.263,.202),(.059,.263,.202),(.055,.329,.199),(-.052,.329,.199)],.02)
        elif kind=="FieldCoat":
            m.loft("olive",[(0,-.43,0,.41,.24),(0,-.35,0,.393,.233),(0,-.12,0,.318,.19),
                   (0,.096,0,.246,.155),(0,.232,0,.295,.151),(0,.286,0,.169,.11)],18,.08)
            m.oval("linen",(0,.278,-.026),(.201,.149,.136),14)
            for s in (-1,1):
                m.ribbon("honey",[(s*.19,-.41,.178),(s*.12,-.17,.193),(s*.069,.10,.17),(s*.135,.269,.103)],
                         [.021,.022,.018,.024],.012)
            m.oval("brass",(0,.129,.165),(.049,.048,.02),10)
        elif kind=="VisoredHelm":
            m.loft("iron",[(0,-.23,0,.32,.266),(0,-.195,0,.344,.291),(0,.001,0,.34,.294),
                   (0,.186,-.018,.286,.254),(0,.291,-.018,.193,.174),(0,.337,-.024,.067,.061)],18)
            m.band("steel",0,-.044,0,.351,.304,.068)
            m.plate("ink",[(-.244,-.06,.211),(-.11,-.09,.294),(0,-.075,.314),(.11,-.09,.294),
                          (.244,-.06,.211),(.201,-.117,.257),(0,-.134,.321),(-.201,-.117,.257)],.018,.004)
            m.plate("steel",[(-.039,.05,.314),(.04,.05,.314),(.034,-.159,.317),(0,-.214,.329),(-.034,-.159,.317)],.024,.008)
        elif kind in ("IronOre","CoalHeart"):
            for i in range(6):
                a=i*2.39
                x,z=.224*cos(a),.183*sin(a)
                h=.15+.073*(i%4)
                m.loft(("basalt" if i%2 else "crust") if kind=="CoalHeart" else ("iron" if i%2 else "steel"),
                      [(x,-.27,z,.074,.078),(x,-.189,z,.16,.135),(x+.022,h-.107,z+.014,.143,.121),
                       (x+.056,h,z+.029,.049,.046)],7)
                if kind=="CoalHeart":
                    m.ribbon("lava",[(x-.063,-.189,z+.108),(x+.04,-.056,z+.145),(x+.017,h-.079,z+.141)],
                             [.012,.018,.009],.008)
        else:
            # Two crescent jaws on a bowed steel spring.
            m.band("iron",0,-.20,0,.44,.32,.084,24)
            m.band("grain",0,-.141,0,.21,.154,.043,14)
            for i in range(14):
                a=2*pi*i/14
                x,z=.39*cos(a),.28*sin(a)
                m.loft("steel",[(x,-.175,z,.059,.05),(x*.99,-.105,z*.99,.059,.049),
                         (x*.88,.137+(i%2)*.038,z*.88,.004,.003)],6)
            for s in (-1,1): m.oval("brass",(s*.394,-.126,0),(.064,.065,.046),10)
        m.save(kind)

def shipping_crate():
    m=Model()
    def board(color,center,sz,turn=0,tilt=0):
        cx,cy,cz=center
        w,h,d=sz
        def point(x,y,z):
            xx=x*cos(tilt)-y*sin(tilt)
            yy=x*sin(tilt)+y*cos(tilt)
            return m.point((cx+xx*cos(turn)+z*sin(turn),cy+yy,cz-xx*sin(turn)+z*cos(turn)))
        bevel=min(.014,w*.11,h*.11,d*.22)
        w,h=w/2,h/2
        rim=[(-w+bevel,-h),(w-bevel,-h),(w,-h+bevel),(w,h-bevel),(w-bevel,h),(-w+bevel,h),(-w,h-bevel),(-w,-h+bevel)]
        rings=[[point(x*f,y*f,z) for x,y in rim] for f,z in [(.98,-d/2),(1,-d/2+bevel),(1,d/2-bevel),(.98,d/2)]]
        m.face(rings[0][::-1],color)
        m.face(rings[-1],color)
        for a,b in zip(rings,rings[1:]):
            for i in range(8):
                j=(i+1)%8
                m.face([a[i],a[j],b[j],b[i]],color)
    for i in range(4):
        v=(i-1.5)*.245
        for side in (-1,1):
            board("grain" if i%2 else "honey",(0,v,side*.463),(.97,.234,.071))
            board("grain" if i%3 else "leather",(side*.462,v,0),(.94,.232,.069),pi/2)
        board("honey" if i%2 else "grain",(v,.467,0),(.236,.066,.96))
        board("oak",(v,-.47,0),(.239,.058,.96))
    for side in (-1,1):
        board("oak",(0,0,side*.509),(.115,1.18,.040),tilt=side*.665)
        for strip in (-1,1):
            board("iron",(strip*.395,0,side*.504),(.109,.999,.038))
            board("iron",(strip*.395,side*.49,0),(.111,.038,1.04))
            for y in (-.394,.394):
                board("steel",(strip*.395,y,side*.526),(.044,.044,.012),tilt=pi/4)
    board("brass",(0,.352,.54),(.172,.17,.025))
    board("oak",(0,.339,.556),(.028,.066,.008))
    m.save("CargoBox")

