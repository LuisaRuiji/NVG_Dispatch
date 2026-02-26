import { useEffect, useRef, useState } from 'react';
import { gsap } from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
import { ArrowRight, Activity, CheckCircle2, Package, ChevronRight, Menu, X } from 'lucide-react';

gsap.registerPlugin(ScrollTrigger);

/* ═══════════════════════════════════════════════════
   NAVBAR
═══════════════════════════════════════════════════ */
function Navbar() {
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);
  const navRef = useRef(null);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 80);
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  const links = ['Platform', 'Features', 'Protocol', 'Pricing'];

  return (
    <nav
      ref={navRef}
      className={`fixed top-5 left-1/2 -translate-x-1/2 z-50 transition-all duration-500 w-[90%] max-w-5xl ${scrolled
          ? 'bg-[#0D0D12]/80 backdrop-blur-2xl border border-[rgba(201,168,76,0.2)] shadow-2xl'
          : 'bg-transparent border border-white/5 backdrop-blur-sm'
        }`}
      style={{ borderRadius: '9999px', padding: '0.65rem 1.5rem' }}
    >
      <div className="flex items-center justify-between">
        {/* Logo */}
        <span className="font-heading font-bold text-lg tracking-tight" style={{ color: scrolled ? '#C9A84C' : '#FAF8F5' }}>
          NVG<span style={{ color: '#C9A84C' }}>.</span>
        </span>

        {/* Desktop links */}
        <ul className="hidden md:flex items-center gap-8">
          {links.map(l => (
            <li key={l}>
              <a href={`#${l.toLowerCase()}`} className="nav-link text-sm font-medium" style={{ color: '#FAF8F5', opacity: 0.7 }}>
                {l}
              </a>
            </li>
          ))}
        </ul>

        {/* CTA */}
        <div className="hidden md:flex">
          <button className="btn-magnetic btn-primary text-sm py-2 px-5" id="nav-cta">
            <span className="relative z-10">Request a Demo</span>
            <span className="btn-bg"></span>
          </button>
        </div>

        {/* Mobile hamburger */}
        <button className="md:hidden text-white/70 hover:text-white transition-colors" onClick={() => setOpen(!open)}>
          {open ? <X size={20} /> : <Menu size={20} />}
        </button>
      </div>

      {/* Mobile menu */}
      {open && (
        <div className="md:hidden mt-4 pb-4 border-t border-white/10 pt-4 flex flex-col gap-4">
          {links.map(l => (
            <a key={l} href={`#${l.toLowerCase()}`} className="text-sm font-medium text-white/70 hover:text-[#C9A84C] transition-colors" onClick={() => setOpen(false)}>
              {l}
            </a>
          ))}
          <button className="btn-magnetic btn-primary text-sm py-2 px-5 self-start" onClick={() => setOpen(false)}>
            <span className="relative z-10">Request a Demo</span>
            <span className="btn-bg"></span>
          </button>
        </div>
      )}
    </nav>
  );
}

/* ═══════════════════════════════════════════════════
   HERO
═══════════════════════════════════════════════════ */
function Hero() {
  const heroRef = useRef(null);

  useEffect(() => {
    const ctx = gsap.context(() => {
      gsap.fromTo('.hero-item', { y: 40, opacity: 0 }, {
        y: 0, opacity: 1, stagger: 0.08, duration: 1.1,
        ease: 'power3.out', delay: 0.3
      });
    }, heroRef);
    return () => ctx.revert();
  }, []);

  return (
    <section
      ref={heroRef}
      id="platform"
      className="relative w-full"
      style={{ height: '100dvh', minHeight: '700px' }}
    >
      {/* Background image */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{
          backgroundImage: `url('https://images.unsplash.com/photo-1618221195710-dd6b41faaea6?w=1800&q=80&fit=crop')`,
        }}
      />
      {/* Gradient overlay */}
      <div className="absolute inset-0" style={{
        background: 'linear-gradient(to top, #0D0D12 30%, rgba(13,13,18,0.6) 65%, rgba(13,13,18,0.2) 100%)'
      }} />

      {/* Content */}
      <div className="absolute bottom-0 left-0 w-full px-8 md:px-16 pb-20 md:pb-28 max-w-4xl">
        <p className="hero-item font-mono text-xs tracking-widest mb-6" style={{ color: '#C9A84C', opacity: 0.8 }}>
          NVG, INC. &nbsp;/&nbsp; B2B GROWTH SYSTEMS
        </p>

        <h1 className="hero-item">
          <span className="font-heading font-black text-5xl md:text-7xl xl:text-8xl leading-none tracking-tight block text-white">
            Precision meets
          </span>
          <span className="font-drama italic text-6xl md:text-8xl xl:text-9xl leading-none block" style={{ color: '#C9A84C' }}>
            Clarity.
          </span>
        </h1>

        <p className="hero-item mt-6 text-base md:text-lg font-light max-w-xl leading-relaxed" style={{ color: 'rgba(250,248,245,0.65)' }}>
          We build growth systems for small to medium B2B companies — starting with real-time inventory, workflow approvals, and kit-ready asset tracking.
        </p>

        <div className="hero-item mt-10 flex flex-wrap gap-4">
          <button className="btn-magnetic btn-primary text-sm px-7 py-3.5" id="hero-cta">
            <span className="relative z-10 flex items-center gap-2">Request a Demo <ArrowRight size={16} /></span>
            <span className="btn-bg"></span>
          </button>
          <button className="btn-magnetic btn-ghost text-sm px-7 py-3.5" id="hero-secondary">
            <span className="relative z-10">See how it works</span>
            <span className="btn-bg"></span>
          </button>
        </div>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════════════════
   FEATURES — 3 Interactive Cards
═══════════════════════════════════════════════════ */

// Card 1 — Diagnostic Shuffler (Real-time inventory clarity)
function ShufflerCard() {
  const labels = ['Stock on hand', 'Low-stock alerts', 'Reorder triggers'];
  const [items, setItems] = useState(labels);

  useEffect(() => {
    const id = setInterval(() => {
      setItems(prev => {
        const next = [...prev];
        next.unshift(next.pop());
        return next;
      });
    }, 3000);
    return () => clearInterval(id);
  }, []);

  const colors = ['#C9A84C', 'rgba(201,168,76,0.55)', 'rgba(201,168,76,0.25)'];
  const scales = [1, 0.97, 0.94];
  const tops = [0, 12, 24];

  return (
    <div className="feature-card p-8 h-80 flex flex-col justify-between relative">
      <div>
        <p className="font-mono text-xs tracking-widest mb-3" style={{ color: '#C9A84C', opacity: 0.7 }}>01 — INVENTORY</p>
        <h3 className="font-heading font-bold text-xl text-white">Real‑time <br />inventory clarity</h3>
      </div>
      <div className="relative h-36">
        {items.map((label, i) => (
          <div
            key={label}
            className="shuffler-card absolute left-0 right-0 flex items-center gap-3 rounded-2xl px-4 py-3"
            style={{
              background: i === 0 ? 'rgba(201,168,76,0.12)' : 'rgba(42,42,53,0.6)',
              border: `1px solid ${i === 0 ? 'rgba(201,168,76,0.4)' : 'rgba(255,255,255,0.06)'}`,
              top: `${tops[i]}px`,
              transform: `scale(${scales[i]})`,
              transformOrigin: 'center top',
              opacity: i === 2 ? 0.5 : 1,
              zIndex: 3 - i,
            }}
          >
            <div className="w-2 h-2 rounded-full" style={{ background: colors[i] }} />
            <span className="font-heading text-sm font-medium" style={{ color: i === 0 ? '#FAF8F5' : 'rgba(250,248,245,0.5)' }}>
              {label}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

// Card 2 — Telemetry Typewriter (Workflow-driven approvals)
function TypewriterCard() {
  const messages = [
    'PR-0042 submitted for approval…',
    'Approver notified via workflow…',
    'Status: APPROVED ✓',
    'Stock updated automatically…',
    'Audit trail logged at 23:41…',
    'PR-0043 queued for review…',
  ];
  const [msgIdx, setMsgIdx] = useState(0);
  const [text, setText] = useState('');
  const [charIdx, setCharIdx] = useState(0);

  useEffect(() => {
    const msg = messages[msgIdx];
    if (charIdx < msg.length) {
      const t = setTimeout(() => {
        setText(msg.slice(0, charIdx + 1));
        setCharIdx(c => c + 1);
      }, 45);
      return () => clearTimeout(t);
    } else {
      const t = setTimeout(() => {
        setCharIdx(0);
        setText('');
        setMsgIdx(m => (m + 1) % messages.length);
      }, 1400);
      return () => clearTimeout(t);
    }
  }, [charIdx, msgIdx]);

  const log = messages.slice(0, msgIdx).slice(-4);

  return (
    <div className="feature-card p-8 h-80 flex flex-col justify-between">
      <div>
        <p className="font-mono text-xs tracking-widest mb-3" style={{ color: '#C9A84C', opacity: 0.7 }}>02 — APPROVALS</p>
        <h3 className="font-heading font-bold text-xl text-white">Workflow‑driven <br />approvals</h3>
      </div>
      <div
        className="rounded-xl p-4 font-mono text-xs flex-1 mt-4 overflow-hidden flex flex-col justify-end"
        style={{ background: '#08080f', border: '1px solid rgba(201,168,76,0.15)' }}
      >
        <div className="flex items-center gap-2 mb-3">
          <span className="w-1.5 h-1.5 rounded-full bg-green-400 pulse-dot" />
          <span className="font-mono text-xs" style={{ color: 'rgba(250,248,245,0.4)' }}>LIVE FEED</span>
        </div>
        {log.map((m, i) => (
          <div key={i} className="mb-1" style={{ color: 'rgba(250,248,245,0.35)', lineHeight: 1.6 }}>
            <span style={{ color: 'rgba(201,168,76,0.5)' }}>›</span> {m}
          </div>
        ))}
        <div style={{ color: '#FAF8F5', lineHeight: 1.6 }}>
          <span style={{ color: '#C9A84C' }}>›</span> {text}
          <span className="cursor-blink" style={{ color: '#C9A84C' }}>|</span>
        </div>
      </div>
    </div>
  );
}

// Card 3 — Cursor Protocol Scheduler (Kit-ready asset tracking)
function SchedulerCard() {
  const days = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];
  const [active, setActive] = useState(3);

  useEffect(() => {
    let idx = 3;
    const id = setInterval(() => {
      idx = (idx + 1) % 7;
      setActive(idx);
    }, 800);
    return () => clearInterval(id);
  }, []);

  return (
    <div className="feature-card p-8 h-80 flex flex-col justify-between">
      <div>
        <p className="font-mono text-xs tracking-widest mb-3" style={{ color: '#C9A84C', opacity: 0.7 }}>03 — ASSETS</p>
        <h3 className="font-heading font-bold text-xl text-white">Kit‑ready <br />asset tracking</h3>
      </div>
      <div className="mt-4 space-y-4">
        <div className="flex gap-2 justify-between">
          {days.map((d, i) => (
            <div
              key={i}
              className="flex-1 py-2.5 rounded-xl flex items-center justify-center font-mono text-xs font-medium transition-all duration-300"
              style={{
                background: active === i ? 'rgba(201,168,76,0.18)' : 'rgba(255,255,255,0.04)',
                border: `1px solid ${active === i ? 'rgba(201,168,76,0.5)' : 'rgba(255,255,255,0.06)'}`,
                color: active === i ? '#C9A84C' : 'rgba(250,248,245,0.35)',
                transform: active === i ? 'scale(1.05)' : 'scale(1)',
              }}
            >
              {d}
            </div>
          ))}
        </div>
        <div className="rounded-xl px-4 py-3 flex items-center justify-between"
          style={{ background: 'rgba(201,168,76,0.08)', border: '1px solid rgba(201,168,76,0.2)' }}>
          <span className="font-heading text-sm text-white/70">Kit dispatch scheduled</span>
          <CheckCircle2 size={16} style={{ color: '#C9A84C' }} />
        </div>
        <button className="w-full py-2 rounded-xl font-heading text-xs font-semibold transition-all duration-200 hover:scale-[1.01]"
          style={{ background: 'rgba(201,168,76,0.15)', border: '1px solid rgba(201,168,76,0.3)', color: '#C9A84C' }}>
          Save Schedule
        </button>
      </div>
    </div>
  );
}

function Features() {
  const ref = useRef(null);
  useEffect(() => {
    const ctx = gsap.context(() => {
      gsap.fromTo('.feature-reveal', { y: 50, opacity: 0 }, {
        y: 0, opacity: 1, stagger: 0.15, duration: 1,
        ease: 'power3.out',
        scrollTrigger: { trigger: ref.current, start: 'top 75%' }
      });
    }, ref);
    return () => ctx.revert();
  }, []);

  return (
    <section id="features" ref={ref} className="py-28 px-6 md:px-12 max-w-7xl mx-auto">
      <div className="feature-reveal mb-16 max-w-xl">
        <p className="font-mono text-xs tracking-widest mb-4" style={{ color: '#C9A84C' }}>INTERACTIVE FEATURES</p>
        <h2 className="font-heading font-black text-4xl md:text-5xl leading-tight text-white">
          Built for the way <br />your team actually works.
        </h2>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
        <div className="feature-reveal"><ShufflerCard /></div>
        <div className="feature-reveal"><TypewriterCard /></div>
        <div className="feature-reveal"><SchedulerCard /></div>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════════════════
   PHILOSOPHY — The Manifesto
═══════════════════════════════════════════════════ */
function Philosophy() {
  const ref = useRef(null);
  const wordsRef = useRef(null);

  useEffect(() => {
    const ctx = gsap.context(() => {
      const words = wordsRef.current?.querySelectorAll('.word');
      if (words) {
        gsap.fromTo(words, { y: 30, opacity: 0 }, {
          y: 0, opacity: 1, stagger: 0.06, duration: 0.9,
          ease: 'power3.out',
          scrollTrigger: { trigger: ref.current, start: 'top 65%' }
        });
      }
      gsap.fromTo('.manifesto-sub', { y: 20, opacity: 0 }, {
        y: 0, opacity: 1, duration: 1, ease: 'power3.out', delay: 0.2,
        scrollTrigger: { trigger: ref.current, start: 'top 65%' }
      });
    }, ref);
    return () => ctx.revert();
  }, []);

  const phrase = 'We focus on precision and growth.';
  const words = phrase.split(' ');

  return (
    <section
      ref={ref}
      className="relative py-36 overflow-hidden"
      style={{ background: '#2A2A35' }}
    >
      {/* Texture overlay */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{
          backgroundImage: `url('https://images.unsplash.com/photo-1541123437800-1bb1317badc2?w=1400&q=60&fit=crop')`,
          opacity: 0.06
        }}
      />
      <div className="relative max-w-5xl mx-auto px-6 md:px-12">
        <p className="manifesto-sub font-mono text-xs tracking-widest mb-10" style={{ color: 'rgba(201,168,76,0.6)' }}>
          OUR PHILOSOPHY
        </p>
        <p className="manifesto-sub text-base md:text-lg font-light mb-10 max-w-lg" style={{ color: 'rgba(250,248,245,0.45)' }}>
          Most B2B software focuses on: feature breadth over operational clarity.
        </p>
        <div ref={wordsRef} className="flex flex-wrap gap-x-4 gap-y-2">
          {words.map((w, i) => {
            const isHighlight = w === 'precision' || w === 'growth.';
            return (
              <span
                key={i}
                className={`word font-drama text-4xl md:text-6xl xl:text-7xl leading-tight ${isHighlight ? 'italic' : ''
                  }`}
                style={{
                  color: isHighlight ? '#C9A84C' : '#FAF8F5',
                  display: 'inline-block',
                }}
              >
                {w}
              </span>
            );
          })}
        </div>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════════════════
   PROTOCOL — Sticky Stacking Cards
═══════════════════════════════════════════════════ */

// SVG 1 — Rotating geometric motif
function GeometricSVG() {
  return (
    <svg width="140" height="140" viewBox="0 0 140 140" fill="none" className="opacity-60">
      <g className="rotate-slow" style={{ transformOrigin: '70px 70px' }}>
        <circle cx="70" cy="70" r="60" stroke="#C9A84C" strokeWidth="1" strokeDasharray="6 4" />
        <circle cx="70" cy="70" r="40" stroke="rgba(201,168,76,0.5)" strokeWidth="1" strokeDasharray="4 6" />
        <polygon points="70,20 114,95 26,95" stroke="rgba(201,168,76,0.4)" strokeWidth="1" fill="none" />
      </g>
      <circle cx="70" cy="70" r="4" fill="#C9A84C" />
    </svg>
  );
}

// SVG 2 — Laser scanner
function LaserScanSVG() {
  const dots = Array.from({ length: 48 }, (_, i) => ({
    x: (i % 8) * 18 + 10,
    y: Math.floor(i / 8) * 18 + 10,
  }));
  return (
    <svg width="160" height="110" viewBox="0 0 160 110" fill="none" className="overflow-hidden opacity-50">
      {dots.map((d, i) => (
        <circle key={i} cx={d.x} cy={d.y} r="2" fill="rgba(201,168,76,0.5)" />
      ))}
      <rect x="-20" y="0" width="30" height="110" rx="2" fill="rgba(201,168,76,0.3)" className="scan-laser" />
    </svg>
  );
}

// SVG 3 — EKG waveform
function WaveformSVG() {
  const path = 'M0,50 L30,50 L40,20 L50,80 L60,50 L90,50 L100,30 L110,70 L120,50 L160,50';
  return (
    <svg width="200" height="100" viewBox="0 0 200 100" fill="none" className="opacity-60">
      <path d={path} stroke="rgba(201,168,76,0.25)" strokeWidth="1.5" fill="none" />
      <path
        d={path}
        stroke="#C9A84C"
        strokeWidth="2"
        fill="none"
        strokeDasharray="400"
        strokeDashoffset="400"
        className="waveform-path"
      />
    </svg>
  );
}

const protocolSteps = [
  {
    num: '01',
    title: 'Capture',
    desc: 'Every item, kit, and asset enters the system with real-time SKU mapping and location tracking from day one.',
    svg: <GeometricSVG />,
    bg: '#13131a',
  },
  {
    num: '02',
    title: 'Review',
    desc: 'Purchase requests flow through structured approval chains — auto-notifying approvers with full audit trails.',
    svg: <LaserScanSVG />,
    bg: '#0D0D12',
  },
  {
    num: '03',
    title: 'Dispatch',
    desc: 'Phase 2 dispatching integration turns approved requests into kit-ready deploy events, instantly visible to the field.',
    svg: <WaveformSVG />,
    bg: '#1a1525',
  },
];

function Protocol() {
  const ref = useRef(null);

  useEffect(() => {
    const ctx = gsap.context(() => {
      const cards = ref.current?.querySelectorAll('.proto-card');
      if (!cards || cards.length < 2) return;

      cards.forEach((card, i) => {
        if (i === cards.length - 1) return;
        ScrollTrigger.create({
          trigger: card,
          start: 'top top',
          end: 'bottom top',
          pin: true,
          pinSpacing: false,
          onUpdate: (self) => {
            const progress = self.progress;
            gsap.to(card, {
              scale: 1 - progress * 0.08,
              filter: `blur(${progress * 8}px)`,
              opacity: 1 - progress * 0.5,
              duration: 0,
            });
          },
        });
      });
    }, ref);
    return () => ctx.revert();
  }, []);

  return (
    <section id="protocol" ref={ref} className="relative">
      <div className="max-w-7xl mx-auto px-6 md:px-12 pt-28 pb-10">
        <div className="mb-16">
          <p className="font-mono text-xs tracking-widest mb-4" style={{ color: '#C9A84C' }}>THE PROTOCOL</p>
          <h2 className="font-heading font-black text-4xl md:text-5xl leading-tight text-white">
            Three stages. <br />
            <span className="font-drama italic" style={{ color: '#C9A84C' }}>Zero gaps.</span>
          </h2>
        </div>
      </div>
      {protocolSteps.map((step, i) => (
        <div
          key={i}
          className="proto-card w-full flex items-center justify-center"
          style={{
            minHeight: '100vh',
            background: step.bg,
            borderTop: '1px solid rgba(201,168,76,0.1)',
          }}
        >
          <div className="max-w-5xl w-full mx-auto px-6 md:px-12 grid grid-cols-1 md:grid-cols-2 gap-12 items-center py-20">
            <div>
              <span className="font-mono text-sm" style={{ color: 'rgba(201,168,76,0.5)' }}>{step.num}</span>
              <h3 className="font-heading font-black text-5xl md:text-7xl mt-2 mb-6 text-white">{step.title}</h3>
              <p className="text-lg font-light leading-relaxed" style={{ color: 'rgba(250,248,245,0.6)' }}>
                {step.desc}
              </p>
              <div className="mt-10 flex items-center gap-3 cursor-pointer group">
                <span className="font-heading text-sm font-semibold" style={{ color: '#C9A84C' }}>
                  Learn more
                </span>
                <ChevronRight size={16} style={{ color: '#C9A84C' }} className="group-hover:translate-x-1 transition-transform" />
              </div>
            </div>
            <div className="flex items-center justify-center">
              <div
                className="rounded-3xl flex items-center justify-center"
                style={{
                  width: 280, height: 280,
                  background: 'rgba(201,168,76,0.05)',
                  border: '1px solid rgba(201,168,76,0.15)',
                }}
              >
                {step.svg}
              </div>
            </div>
          </div>
        </div>
      ))}
    </section>
  );
}

/* ═══════════════════════════════════════════════════
   PRICING / CTA  
═══════════════════════════════════════════════════ */
const plans = [
  {
    name: 'Essential',
    price: 'Custom',
    desc: 'For lean teams starting with inventory visibility and basic approvals.',
    features: ['Real-time stock tracking', 'Single-approver workflows', 'Up to 500 SKUs', 'Email support'],
    featured: false,
  },
  {
    name: 'Performance',
    price: 'Custom',
    desc: 'For growing operations that need multi-step approvals and asset kitting.',
    features: ['Everything in Essential', 'Multi-tier approval chains', 'Kit assembly + dispatch', 'Audit trail export', 'Priority support'],
    featured: true,
  },
  {
    name: 'Enterprise',
    price: 'Custom',
    desc: 'Full-stack growth infrastructure for multi-location B2B operations.',
    features: ['Everything in Performance', 'Unlimited SKUs & locations', 'Phase 2 dispatch integration', 'Dedicated success manager', 'SLA & white-glove onboarding'],
    featured: false,
  },
];

function Pricing() {
  const ref = useRef(null);
  useEffect(() => {
    const ctx = gsap.context(() => {
      gsap.fromTo('.pricing-card', { y: 40, opacity: 0 }, {
        y: 0, opacity: 1, stagger: 0.12, duration: 1,
        ease: 'power3.out',
        scrollTrigger: { trigger: ref.current, start: 'top 70%' }
      });
    }, ref);
    return () => ctx.revert();
  }, []);

  return (
    <section id="pricing" ref={ref} className="py-32 px-6 md:px-12 max-w-7xl mx-auto">
      <div className="mb-16 max-w-xl">
        <p className="font-mono text-xs tracking-widest mb-4" style={{ color: '#C9A84C' }}>MEMBERSHIP</p>
        <h2 className="font-heading font-black text-4xl md:text-5xl leading-tight text-white">
          Invest in clarity. <br />
          <span className="font-drama italic" style={{ color: '#C9A84C' }}>Scale with confidence.</span>
        </h2>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-5 items-center">
        {plans.map((plan) => (
          <div
            key={plan.name}
            className={`pricing-card rounded-3xl p-8 flex flex-col gap-6 ${plan.featured ? 'md:scale-105' : ''
              }`}
            style={{
              background: plan.featured ? '#2A2A35' : '#13131a',
              border: plan.featured
                ? '1px solid rgba(201,168,76,0.5)'
                : '1px solid rgba(255,255,255,0.07)',
              boxShadow: plan.featured ? '0 0 60px rgba(201,168,76,0.08)' : 'none',
            }}
          >
            {plan.featured && (
              <div
                className="self-start px-3 py-1 rounded-full font-mono text-xs font-medium"
                style={{ background: 'rgba(201,168,76,0.15)', color: '#C9A84C', border: '1px solid rgba(201,168,76,0.3)' }}
              >
                MOST POPULAR
              </div>
            )}
            <div>
              <h3 className="font-heading font-bold text-xl text-white mb-2">{plan.name}</h3>
              <p className="text-sm leading-relaxed" style={{ color: 'rgba(250,248,245,0.5)' }}>{plan.desc}</p>
            </div>
            <ul className="flex flex-col gap-3 flex-1">
              {plan.features.map(f => (
                <li key={f} className="flex items-start gap-3">
                  <CheckCircle2 size={15} className="mt-0.5 flex-shrink-0" style={{ color: '#C9A84C' }} />
                  <span className="text-sm" style={{ color: 'rgba(250,248,245,0.65)' }}>{f}</span>
                </li>
              ))}
            </ul>
            <button
              className={`btn-magnetic text-sm py-3 px-6 rounded-full font-semibold ${plan.featured ? 'btn-primary' : 'btn-ghost'
                }`}
              id={`pricing-${plan.name.toLowerCase()}`}
            >
              <span className="relative z-10">Request a Demo</span>
              <span className="btn-bg"></span>
            </button>
          </div>
        ))}
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════════════════
   FOOTER
═══════════════════════════════════════════════════ */
function Footer() {
  const cols = {
    Platform: ['Inventory', 'Approvals', 'Asset Kits', 'Dispatch (Phase 2)'],
    Company: ['About', 'Careers', 'Blog', 'Contact'],
    Legal: ['Privacy', 'Terms', 'Security'],
  };

  return (
    <footer
      className="relative pt-20 pb-10 px-6 md:px-12"
      style={{
        background: '#08080f',
        borderTop: '1px solid rgba(201,168,76,0.1)',
        borderRadius: '4rem 4rem 0 0',
      }}
    >
      <div className="max-w-7xl mx-auto grid grid-cols-1 md:grid-cols-5 gap-12 mb-16">
        {/* Brand */}
        <div className="md:col-span-2">
          <div className="font-heading font-black text-3xl mb-3 text-white">
            NVG<span style={{ color: '#C9A84C' }}>.</span>
          </div>
          <p className="text-sm leading-relaxed max-w-xs" style={{ color: 'rgba(250,248,245,0.45)' }}>
            Growth systems for small to medium B2B companies. Real-time clarity, operational precision.
          </p>
          <div className="mt-6 flex items-center gap-2">
            <span
              className="w-2 h-2 rounded-full bg-green-400 status-pulse"
              style={{ display: 'inline-block' }}
            />
            <span className="font-mono text-xs" style={{ color: 'rgba(250,248,245,0.4)' }}>
              SYSTEM OPERATIONAL
            </span>
          </div>
        </div>

        {/* Nav columns */}
        {Object.entries(cols).map(([group, items]) => (
          <div key={group}>
            <p className="font-mono text-xs tracking-widest mb-5" style={{ color: 'rgba(201,168,76,0.6)' }}>
              {group.toUpperCase()}
            </p>
            <ul className="flex flex-col gap-3">
              {items.map(item => (
                <li key={item}>
                  <a
                    href="#"
                    className="nav-link text-sm"
                    style={{ color: 'rgba(250,248,245,0.5)' }}
                  >
                    {item}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      {/* Bottom bar */}
      <div
        className="max-w-7xl mx-auto pt-6 flex flex-col md:flex-row items-center justify-between gap-4"
        style={{ borderTop: '1px solid rgba(255,255,255,0.06)' }}
      >
        <p className="font-mono text-xs" style={{ color: 'rgba(250,248,245,0.25)' }}>
          © 2026 NVG, Inc. All rights reserved.
        </p>
        <p className="font-mono text-xs" style={{ color: 'rgba(250,248,245,0.25)' }}>
          Phase 1 · Inventory & Approvals
        </p>
      </div>
    </footer>
  );
}

/* ═══════════════════════════════════════════════════
   APP ROOT
═══════════════════════════════════════════════════ */
export default function App() {
  return (
    <div className="min-h-screen" style={{ background: '#0D0D12' }}>
      <Navbar />
      <Hero />
      <Features />
      <Philosophy />
      <Protocol />
      <Pricing />
      <Footer />
    </div>
  );
}
