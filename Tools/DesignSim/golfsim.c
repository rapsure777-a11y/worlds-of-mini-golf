/* Worlds of Mini Golf design simulator: a 2D (top-down) model of GolfBall.cs for paper-checking hole concepts.
 * Not part of the game. Model: rolling resistance a0 + k*v, 5/7 g slope acceleration from a height grid, hold-on-gentle-slope rule,
 * wall rebound v = t*keep - vn*bounce*n from the pre-contact velocity, cup capture = centre inside the cup radius for the time a
 * ball needs to drop one ball-radius*1.2 (0.074 s), out-of-bounds = centre inside a hazard polygon.
 * Build: gcc -O2 -shared -fPIC -o libgolfsim.so golfsim.c -lm */
#include <math.h>
#include <string.h>

typedef struct {
    double a0, k, rest_speed, rest_time, bounce, keep, radius, cup_r, cup_fall, maxspeed;
} Params;

typedef struct {
    int status;      /* 0 at rest, 1 holed, 2 out of bounds, 3 timeout */
    double x, y, t;
    int hits;
    double first_hit_x, first_hit_y;
    double speed_at_cup;
} Result;

static double grid_h(const double *g, int nx, int ny, double x0, double y0, double step, double x, double y) {
    double fx = (x - x0) / step, fy = (y - y0) / step;
    int ix = (int)floor(fx), iy = (int)floor(fy);
    if (ix < 0) ix = 0; if (iy < 0) iy = 0; if (ix > nx - 2) ix = nx - 2; if (iy > ny - 2) iy = ny - 2;
    double tx = fx - ix, ty = fy - iy;
    if (tx < 0) tx = 0; if (tx > 1) tx = 1; if (ty < 0) ty = 0; if (ty > 1) ty = 1;
    double a = g[iy * nx + ix], b = g[iy * nx + ix + 1], c = g[(iy + 1) * nx + ix], d = g[(iy + 1) * nx + ix + 1];
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
}

static int in_poly(const double *px, const double *py, int n, double x, double y) {
    int c = 0;
    for (int i = 0, j = n - 1; i < n; j = i++)
        if (((py[i] > y) != (py[j] > y)) && (x < (px[j] - px[i]) * (y - py[i]) / (py[j] - py[i]) + px[i])) c = !c;
    return c;
}

/* segs: nseg*4 doubles (x1,y1,x2,y2). grid: heights (nx*ny), origin (gx0,gy0), step. haz: concatenated polygon vertices,
 * hazn: vertex counts per polygon. */
void run(const Params *p, int nseg, const double *segs, const double *grid, int gnx, int gny, double gx0, double gy0, double gstep,
         int nhaz, const double *hazx, const double *hazy, const int *hazn, double cupx, double cupy, int has_cup,
         double x, double y, double vx, double vy, double max_t, Result *out) {
    const double dt = 1.0 / 480.0, g = 9.81;
    double t = 0, slow = 0, inside = 0;
    int hits = 0, first = 1;
    memset(out, 0, sizeof(Result));
    for (; t < max_t; t += dt) {
        /* slope */
        double e = 0.02;
        double hx = (grid_h(grid, gnx, gny, gx0, gy0, gstep, x + e, y) - grid_h(grid, gnx, gny, gx0, gy0, gstep, x - e, y)) / (2 * e);
        double hy = (grid_h(grid, gnx, gny, gx0, gy0, gstep, x, y + e) - grid_h(grid, gnx, gny, gx0, gy0, gstep, x, y - e)) / (2 * e);
        double den = 1.0 + hx * hx + hy * hy;
        double ax = -(5.0 / 7.0) * g * hx / den, ay = -(5.0 / 7.0) * g * hy / den;
        double amag = sqrt(ax * ax + ay * ay), speed = sqrt(vx * vx + vy * vy);
        if (speed < p->rest_speed && amag <= p->a0) { vx = vy = 0; }
        else {
            vx += ax * dt; vy += ay * dt;
            double s = sqrt(vx * vx + vy * vy), drop = (p->a0 + p->k * s) * dt;
            if (s > drop) { vx *= 1 - drop / s; vy *= 1 - drop / s; } else { vx = vy = 0; }
        }
        double pvx = vx, pvy = vy;
        x += vx * dt; y += vy * dt;
        /* walls */
        for (int i = 0; i < nseg; i++) {
            double x1 = segs[i * 4], y1 = segs[i * 4 + 1], x2 = segs[i * 4 + 2], y2 = segs[i * 4 + 3];
            double dx = x2 - x1, dy = y2 - y1, l2 = dx * dx + dy * dy;
            double u = l2 > 0 ? ((x - x1) * dx + (y - y1) * dy) / l2 : 0;
            if (u < 0) u = 0; if (u > 1) u = 1;
            double cx = x1 + u * dx, cy = y1 + u * dy;
            double nx = x - cx, ny = y - cy, d = sqrt(nx * nx + ny * ny);
            if (d < p->radius && d > 1e-9) {
                nx /= d; ny /= d;
                double vn = pvx * nx + pvy * ny;
                if (vn < -0.02) {
                    double tx = pvx - vn * nx, ty = pvy - vn * ny;
                    vx = tx * p->keep - vn * p->bounce * nx; vy = ty * p->keep - vn * p->bounce * ny;
                    hits++;
                    if (first) { first = 0; out->first_hit_x = cx; out->first_hit_y = cy; }
                } else { /* resting contact: just push out */ }
                x = cx + nx * p->radius; y = cy + ny * p->radius;
                pvx = vx; pvy = vy;
            }
        }
        /* hazards */
        int off = 0;
        for (int h = 0; h < nhaz; h++) {
            if (in_poly(hazx + off, hazy + off, hazn[h], x, y)) { out->status = 2; out->x = x; out->y = y; out->t = t; out->hits = hits; return; }
            off += hazn[h];
        }
        /* cup */
        if (has_cup) {
            double cdx = x - cupx, cdy = y - cupy;
            if (cdx * cdx + cdy * cdy < p->cup_r * p->cup_r) {
                if (inside == 0) out->speed_at_cup = sqrt(vx * vx + vy * vy);
                inside += dt;
                if (inside >= p->cup_fall) { out->status = 1; out->x = x; out->y = y; out->t = t; out->hits = hits; return; }
            } else inside = 0;
        }
        /* rest */
        double sp = sqrt(vx * vx + vy * vy);
        if (sp < p->rest_speed) { slow += dt; if (slow >= p->rest_time) { out->status = 0; out->x = x; out->y = y; out->t = t; out->hits = hits; return; } }
        else slow = 0;
    }
    out->status = 3; out->x = x; out->y = y; out->t = t; out->hits = hits;
}
