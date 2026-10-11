"""Authoring-only hair regions and deterministic surface direction generation."""
import heapq
import math
from mathutils import Vector

MASK = 'WGameHairRegion'
ROOT = 'WGameHairRoot'
FLOW = 'WGameHairFlow'
PROGRESS = 'WGameHairProgress'
GUIDE = 'wgame_hair_guide'


class HairFlowIssue(ValueError):
    def __init__(self,message,vertices=(),guides=()):
        super().__init__(message)
        self.vertices=tuple(vertices)
        self.guides=tuple(guides)


def attribute(mesh, name, kind, domain):
    a = mesh.attributes.get(name)
    if a and (a.data_type != kind or a.domain != domain):
        raise ValueError(f'{name}: incompatible attribute {a.data_type}/{a.domain}')
    return a or mesh.attributes.new(name=name, type=kind, domain=domain)


def profiles(mesh):
    if mesh.is_editmode:
        import bmesh
        bm=bmesh.from_edit_mesh(mesh)
        layer=bm.faces.layers.int.get('SurfaceProfile')
        if layer:return [int(face[layer]) for face in bm.faces]
        color=bm.loops.layers.float_color.get('PaletteData') or bm.loops.layers.color.get('PaletteData')
        if color:return [min(15,int(face.loops[0][color][3]*16)) for face in bm.faces]
        raise ValueError(f'{mesh.name}: missing PaletteData')
    a = mesh.attributes.get('SurfaceProfile')
    if a and a.domain == 'FACE':
        return [int(v.value) for v in a.data]
    color = mesh.color_attributes.get('PaletteData')
    if not color:
        raise ValueError(f'{mesh.name}: missing PaletteData')
    return [min(15, int(color.data[p.loop_start].color[3]*16)) for p in mesh.polygons]


def region(mesh):
    # Surface type is the only authoring authority. Historical manual masks may
    # remain in older Work files, but must not override newly painted materials.
    return [p in (2,3) for p in profiles(mesh)]


def head_position(obj, rig):
    if rig:
        candidates = [b for b in rig.data.bones if b.use_deform and 'head' in b.name.lower()]
        if candidates:
            b = min(candidates, key=lambda x: (len(x.name), x.name))
            return obj.matrix_world.inverted() @ rig.matrix_world @ b.head_local
    return None


def guide_points(spline):
    """Ordered controls: first is root, last is tip (also used by the overlay)."""
    return [p.co.copy() for p in spline.bezier_points] if spline.type=='BEZIER' else [p.co.xyz.copy() for p in spline.points]


def guide_segments(obj, guides, include_names=False):
    result = []
    for g in sorted(guides, key=lambda x: x.name):
        if g.type != 'CURVE' or not g.get(GUIDE):
            continue
        if g.get('wgame_hair_target') and g.get('wgame_hair_target') != obj.name:
            continue
        transform = obj.matrix_world.inverted() @ g.matrix_world
        for spline in g.data.splines:
            points = [transform @ p for p in guide_points(spline)]
            for a, b in zip(points, points[1:]):
                if (b-a).length > 1e-8:
                    segment=(a,b,(b-a).normalized())
                    result.append(segment+(g.name,) if include_names else segment)
    return result


def generate(obj, rig=None, guides=()):
    """Writes source-space vectors; final export encodes them in the BakeUV basis."""
    mesh = obj.data
    flags = region(mesh)
    coords = [v.co.copy() for v in mesh.vertices]
    graph = {}
    for face, enabled in zip(mesh.polygons, flags):
        if enabled:
            ids = list(face.vertices)
            for a, b in zip(ids, ids[1:]+ids[:1]):
                w = (coords[a]-coords[b]).length
                graph.setdefault(a, {})[b] = w
                graph.setdefault(b, {})[a] = w
    if not graph:
        flow=mesh.attributes.get(FLOW);progress=mesh.attributes.get(PROGRESS)
        if flow:
            for item in flow.data:item.vector=(0,0,0)
        if progress:
            for item in progress.data:item.value=0
        return {'vertices': 0, 'components': 0, 'automaticRoots': 0}
    components = []
    remaining = set(graph)
    while remaining:
        seed = min(remaining)
        stack, component = [seed], set()
        remaining.remove(seed)
        while stack:
            v = stack.pop()
            component.add(v)
            for n in sorted(graph[v]):
                if n in remaining:
                    remaining.remove(n)
                    stack.append(n)
        components.append(component)
    roots = mesh.attributes.get(ROOT)
    center = head_position(obj, rig)
    segments = guide_segments(obj, guides,include_names=True)
    distances, progress = {}, {}
    auto_roots = 0
    for component in components:
        seeds = sorted(v for v in component if roots and roots.data[v].value)
        if not seeds:
            if center is None and not segments:
                raise HairFlowIssue(f'{obj.name}: hair component has no root; mark roots or add a guide',component)
            starts=[a for a,b,d,name in segments]
            guide_root=min(starts,key=lambda a:min((coords[v]-a).length_squared for v in component)) if starts else None
            target = guide_root if guide_root is not None else center
            seed = min(component, key=lambda v: ((coords[v]-target).length_squared, v))
            seeds = [seed]
            auto_roots += 1
        dist = {v: math.inf for v in component}
        queue = []
        for v in seeds:
            dist[v] = 0.0
            heapq.heappush(queue, (0.0, v))
        while queue:
            d, v = heapq.heappop(queue)
            if d != dist[v]:
                continue
            for n, length in sorted(graph[v].items()):
                nd = d+length
                if nd < dist[n]:
                    dist[n] = nd
                    heapq.heappush(queue, (nd, n))
        maximum = max(dist.values())
        if not math.isfinite(maximum) or maximum < 1e-8:
            raise HairFlowIssue(f'{obj.name}: degenerate hair component; check root marks',component)
        distances.update(dist)
        progress.update({v: d/maximum for v, d in dist.items()})
    vectors = {v: Vector((0, 0, 0)) for v in graph}
    mesh.calc_loop_triangles()
    for tri in mesh.loop_triangles:
        if not flags[tri.polygon_index]:
            continue
        a,b,c = tri.vertices
        e,f = coords[b]-coords[a],coords[c]-coords[a]
        ee,ff,ef = e.dot(e),f.dot(f),e.dot(f)
        determinant = ee*ff-ef*ef
        if determinant <= 1e-20:
            continue
        u,v = distances[b]-distances[a],distances[c]-distances[a]
        gradient = e*((u*ff-v*ef)/determinant)+f*((v*ee-u*ef)/determinant)
        for vertex in (a,b,c):
            vectors[vertex] += gradient*tri.area
    pins = {}
    radius = max((max(coords[v][i] for v in graph)-min(coords[v][i] for v in graph)) for i in range(3))*0.15
    for v in graph:
        p = coords[v]
        nearest = None
        for a,b,direction,guide_name in segments:
            edge = b-a
            q = a+edge*max(0,min(1,(p-a).dot(edge)/edge.length_squared))
            distance = (p-q).length
            # Nearby, spatially separate locks may intentionally flow in opposite
            # directions. Only coincident strong constraints are contradictory;
            # an equidistant vertex between separate guides is a nearest-guide
            # boundary, not an authoring error.
            if nearest is not None and abs(distance-nearest[0]) < radius*.05 and max(distance,nearest[0]) <= radius*.25 and (q-nearest[3]).length <= radius*.1 and direction.dot(nearest[1]) < -.5:
                names=tuple(sorted({nearest[2],guide_name}))
                raise HairFlowIssue(f'引导线方向冲突（顶点 {v}）：'+ ' / '.join(names),[v],names)
            if distance <= radius and (nearest is None or distance < nearest[0]):
                nearest = (distance,direction,guide_name,q)
        if nearest:
            pins[v] = nearest[1]
            vectors[v] = nearest[1].copy()
        elif vectors[v].length < 1e-10:
            n = max(graph[v], key=lambda n: (abs(distances[n]-distances[v]),n))
            vectors[v] = (coords[n]-p)*(1 if distances[n]>=distances[v] else -1)
    for _ in range(8):
        next_vectors = {}
        for v in sorted(graph):
            normal = mesh.vertices[v].normal
            value = pins.get(v)
            if value is None:
                value = vectors[v].normalized()*2
                for n in graph[v]:
                    value += vectors[n].normalized()/max(1,len(graph[v]))
            value = value-normal*normal.dot(value)
            if value.length < 1e-8:
                value = vectors[v]-normal*normal.dot(vectors[v])
            if value.length < 1e-8:
                raise HairFlowIssue(f'{obj.name}: invalid hair direction at vertex {v}; add guide',[v])
            next_vectors[v] = value.normalized()
        vectors = next_vectors
    flow = attribute(mesh,FLOW,'FLOAT_VECTOR','POINT')
    length = attribute(mesh,PROGRESS,'FLOAT','POINT')
    for i in range(len(mesh.vertices)):
        flow.data[i].vector = vectors.get(i,Vector((0,0,0)))
        length.data[i].value = progress.get(i,0.0)
    return {'vertices':len(graph),'components':len(components),'automaticRoots':auto_roots}
