"""Remove auxiliary tangent layers from baked binary FBX without changing payloads.

UV0/HairFlow/HairMeta, normals, skinning and the UV0 tangent basis remain byte exact.
Only FBX node offsets and footer alignment are rewritten. No mesh re-export occurs.
"""
from pathlib import Path
import os
import struct
import tempfile

MAGIC = b'Kaydara FBX Binary  \x00\x1a\x00'
TSPACE = {b'LayerElementTangent', b'LayerElementBinormal'}


def read_document(data):
    if data[:23] != MAGIC:raise ValueError('Expected binary FBX')
    version, = struct.unpack_from('<I', data, 23)
    if version not in (7400, 7500):raise ValueError('Unsupported FBX version')
    header = struct.Struct('<QQQB' if version == 7500 else '<IIIB')
    def read(offset, limit):
        if offset + header.size > limit:raise ValueError('Truncated FBX node')
        end, count, length, size = header.unpack_from(data, offset)
        if end == 0:
            if data[offset:offset+header.size] != bytes(header.size):raise ValueError('Invalid FBX terminator')
            return None, offset + header.size
        start = offset + header.size
        props_end = start + size + length
        if not props_end <= end <= limit:raise ValueError('Invalid FBX node offsets')
        node = {'name':data[start:start+size], 'count':count, 'props':data[start+size:props_end], 'children':[], 'terminated':False}
        offset = props_end
        while offset < end:
            child, offset = read(offset, end)
            if child is None:
                node['terminated'] = True
                break
            node['children'].append(child)
        if offset != end:raise ValueError('Invalid FBX child boundary')
        return node, end
    roots, offset = [], 27
    while True:
        node, offset = read(offset, len(data))
        if node is None:break
        roots.append(node)
    footer = data[offset:]
    padding = len(footer) - 160
    if not (1 <= padding <= 16 and footer[16:20+padding] == bytes(4+padding)
            and footer[20+padding:24+padding] == struct.pack('<I',version)):
        raise ValueError('Unsupported FBX footer')
    return roots, version, footer


def scalar(node, kind):
    props = node['props']
    if node['count'] != 1 or props[:1] != kind:raise ValueError('Unexpected FBX scalar')
    if kind == b'I' and len(props) == 5:return struct.unpack_from('<i',props,1)[0]
    if kind == b'S' and len(props) >= 5:
        length, = struct.unpack_from('<I',props,1)
        if length == len(props)-5:return props[5:]
    raise ValueError('Malformed FBX scalar')


def child(node, name):
    found = [n for n in node['children'] if n['name'] == name]
    if len(found) != 1:raise ValueError('Expected exactly one FBX ' + name.decode())
    return found[0]


def prune_layers(roots):
    objects = child({'children':roots},b'Objects')
    meshes, removed = 0, 0
    for geometry in objects['children']:
        if geometry['name'] != b'Geometry':continue
        uvs = [n for n in geometry['children'] if n['name'] == b'LayerElementUV']
        if not uvs:continue  # Blend shape geometry has no UV/tangent layers.
        names = {scalar(n,b'I'):scalar(child(n,b'Name'),b'S') for n in uvs}
        if len(uvs) != 3 or names != {0:b'UV0',1:b'HairFlow',2:b'HairMeta'}:
            raise ValueError('Tangent pruning requires baked UV0/HairFlow/HairMeta')
        for kind in TSPACE:
            indices = [scalar(n,b'I') for n in geometry['children'] if n['name'] == kind]
            if indices not in ([0],[0,1,2]):raise ValueError('Unexpected FBX tangent layers')
        kept = []
        for n in geometry['children']:
            if n['name'] in TSPACE and scalar(n,b'I') != 0:
                removed += 1;continue
            if n['name'] == b'Layer':
                n['children'] = [ref for ref in n['children'] if not (
                    ref['name'] == b'LayerElement' and scalar(child(ref,b'Type'),b'S') in TSPACE
                    and scalar(child(ref,b'TypedIndex'),b'I') != 0)]
            kept.append(n)
        geometry['children'] = kept
        meshes += 1
    if not meshes:raise ValueError('No baked mesh found')
    return meshes, removed


def write_document(roots, version, footer):
    header = struct.Struct('<QQQB' if version == 7500 else '<IIIB')
    output = bytearray(MAGIC + struct.pack('<I',version))
    def write(node):
        start = len(output);output.extend(bytes(header.size));output.extend(node['name']);output.extend(node['props'])
        for entry in node['children']:write(entry)
        if node['terminated']:output.extend(bytes(header.size))
        header.pack_into(output,start,len(output),node['count'],len(node['props']),len(node['name']))
    for entry in roots:write(entry)
    output.extend(bytes(header.size));output.extend(footer[:20])
    output.extend(bytes(16-len(output)%16))
    output.extend(footer[len(footer)-140:])
    return bytes(output)


def optimize_bytes(data):
    roots, version, footer = read_document(data)
    meshes, removed = prune_layers(roots)
    result = write_document(roots,version,footer) if removed else data
    check, _, _ = read_document(result)
    if check != roots:raise ValueError('FBX payload preservation check failed')
    return result, {'meshes':meshes,'removedTangentLayers':removed,'beforeBytes':len(data),'afterBytes':len(result)}


def optimize_file(source, destination=None):
    source = Path(source);destination = Path(destination) if destination else source
    result, report = optimize_bytes(source.read_bytes())
    if source == destination and not report['removedTangentLayers']:return report
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=destination.parent,prefix=destination.name+'.',delete=False) as stream:
            temporary = Path(stream.name);stream.write(result);stream.flush();os.fsync(stream.fileno())
        os.replace(temporary,destination)
    finally:
        if temporary is not None:temporary.unlink(missing_ok=True)
    return report
