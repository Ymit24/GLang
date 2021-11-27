
syntax on
set noerrorbells
set tabstop=4 softtabstop=4
set shiftwidth=4
set smartindent
set nu
set nowrap
set smartcase
set noswapfile
set nobackup
set incsearch
set colorcolumn=120
set shortmess+=c

filetype plugin on
"set omnifunc=syntaxcomplete#Complete

"let g:python3_host_prog = expand('C:\Users\chris\AppData\Local\Programs\Python\Python38\python.exe')

highlight ColorColumn ctermbg=0 guibg=lightgrey
call plug#begin('~/.config/nvim/plugged')

Plug 'morhetz/gruvbox'
" Plug 'nvim-treesitter/nvim-treesitter'
"Plug 'jremmen/vim-ripgrep'
"Plug 'tpope/vim-fugitive'
"Plug 'vim-utils/vim-man'
"Plug 'mbbill/undotree'
"Plug 'junegunn/fzf', { 'do': { -> fzf#install() } }
"Plug 'junegunn/fzf.vim'
"Plug 'dense-analysis/ale'

Plug 'preservim/nerdtree'
Plug 'OmniSharp/omnisharp-vim'

Plug 'neoclide/coc.nvim', {'branch':'release'}
Plug 'colepeters/spacemacs-theme.vim'
Plug 'challenger-deep-theme/vim', {'name':'challenger-deep-theme'}

if has('nvim')
	Plug 'Shougo/deoplete.nvim', { 'do': ':UpdateRemotePlugins' }
else
	"Plug 'Shougo/deoplete.nvim'
	"Plug 'roxma/nvim-yarp'
	"Plug 'roxma/vim-hug-neovim-rpc'
endif
Plug 'dracula/vim',{'as':'dracula'}

set number

call plug#end()

let mapleader = " "
let g:netrw_browse_split=2
let g:netrw_banner=0
let g:netrw_winsize=25

let OmniSharp_translate_cygwin_wsl = 0

let g:fzf_layout = {'window': { 'width': 0.8, 'height': 0.8 } }
let $FZF_DEFAULT_OPTS = '--reverse'

" nnoremap <leader>prw :CocSearch <C-R>=expand("<cword>")<CR><CR>
nnoremap <leader>h :wincmd h<CR>
nnoremap <leader>j :wincmd j<CR>
nnoremap <leader>k :wincmd k<CR>
nnoremap <leader>l :wincmd l<CR>
nnoremap <leader>pv :wincmd v<bar> :Ex <bar> :vertical resize 30<CR>
nnoremap <leader>ps :Rg<SPACE>

nnoremap <leader>n :NERDTreeFocus<CR>
nnoremap <leader>nt :NERDTreeToggle<CR>
nnoremap <leader>nf :NERDTreeFind<CR>

autocmd FileType cs nnoremap <leader>gu :OmniSharpFindUsages<CR>
autocmd FileType cs nnoremap <leader>pd :OmniSharpPreviewDefinition<CR>
autocmd FileType cs nnoremap <leader>gi :OmniSharpFindImplementations<CR>
autocmd FileType cs nnoremap <leader>gd :OmniSharpGotoDefinition<CR>

"nmap <silent> ga <Plug>(coc-action)
nnoremap <leader>ga :CocAction<CR>
autocmd FileType cpp nmap <silent> gd <Plug>(coc-definition)
autocmd FileType cpp nmap <silent> gt <Plug>(coc-type-definition)
autocmd FileType cpp nmap <silent> gr <Plug>(coc-rename)
autocmd FileType cpp nmap <silent> gi <Plug>(coc-implementation)

" colorscheme challenger_deep
" colorscheme spacemacs-theme
colorscheme gruvbox
nnoremap <C-p> :GFiles<CRt
set relativenumber

nnoremap <space><space> :let @/=""<CR>

autocmd FileType c,cpp setlocal equalprg=clang-format

" Always show the signcolumn, otherwise it would shift the text each time
" diagnostics appear/become resolved.
if has("nvim-0.5.0") || has("patch-8.1.1564")
  " Recently vim can merge signcolumn and number column into one
  set signcolumn=number
else
  set signcolumn=yes
endif

" Use tab for trigger completion with characters ahead and navigate.
" NOTE: Use command ':verbose imap <tab>' to make sure tab is not mapped by
" other plugin before putting this into your config.
inoremap <silent><expr> <TAB>
      \ pumvisible() ? "\<C-n>" :
      \ <SID>check_back_space() ? "\<TAB>" :
      \ coc#refresh()
inoremap <expr><S-TAB> pumvisible() ? "\<C-p>" : "\<C-h>"

function! s:check_back_space() abort
  let col = col('.') - 1
  return !col || getline('.')[col - 1]  =~# '\s'
endfunction


" Use <c-space> to trigger completion.
if has('nvim')
  inoremap <silent><expr> <c-space> coc#refresh()
else
  inoremap <silent><expr> <c-@> coc#refresh()
endif


" Highlight the symrol and its references when holding the cursor.
autocmd CursorHold * silent call CocActionAsync('highlight')
