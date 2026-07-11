import type { ImgHTMLAttributes } from 'react';

export type BrandMarkProps = Omit<ImgHTMLAttributes<HTMLImageElement>, 'alt' | 'src'>;

export function BrandMark({ className, ...props }: BrandMarkProps) {
  return (
    <span className={['brand-mark', className].filter(Boolean).join(' ')}>
      <img src="/referentia-symbol.svg" alt="" {...props} />
    </span>
  );
}
