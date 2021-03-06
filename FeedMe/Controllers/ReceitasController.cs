﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FeedMe.Models;
using FeedMe.DTO;
using System.IO;

namespace FeedMe.Controllers
{
    public class ReceitasController : Controller
    {
        private DatabaseContext db = new DatabaseContext();

		#region Index
		//
        // GET: /Receitas/

        public ActionResult Index()
        {
            var receitas = db.Receitas.Include(r => r.Tipo).Include(r => r.Utilizador).Include(r => r.Origem);
            return View(receitas.ToList());
        }

		#endregion Index

		#region Details
		//
        // GET: /Receitas/Details/5

        public ActionResult Details(int id = 0)
        {
            Receita receita = db.Receitas.Find(id);
            if (receita == null)
            {
                return HttpNotFound();
            }
            return View(receita);
        }

		#endregion Details

		#region Create

		//
        // GET: /Receitas/Create

		[Authorize]
        public ActionResult Create()
		{
			if ( db.isAdmin(User.Identity.Name) )
				return RedirectToAction("Error", "Home", new { Message = "Administradores não podem criar receitas." }); 

            ViewBag.TipoId = new SelectList(db.Tipos, "TipoId", "Nome");
			ViewBag.UtilizadorId = new SelectList(db.Utilizadores, "UtilizadorId", "Nome");
			ViewBag.OrigemId = new SelectList(db.Origens, "OrigemId", "Nome");
			ViewBag.DificuldadeId = new SelectList(db.Dificuldades, "DificuldadeId", "Nome");
			ViewBag.CustoId = new SelectList(db.Custos, "CustoId", "Nome");
            return View();
        }

		//
        // POST: /Receitas/Create

		[HttpPost]
		[Authorize]
        public ActionResult Create(Receita receita)
        {
			var ra = Request.Url.PathAndQuery;
			Utilizador u =
				(from x in db.Utilizadores
				where x.Nome == User.Identity.Name
				select x).First();

            if (ModelState.IsValid)
            {
				receita.UtilizadorId = u.UtilizadorId;
                db.Receitas.Add(receita);
				db.SaveChanges();
				return RedirectToAction("Ingredientes", new { id = receita.ReceitaId });
            }

            ViewBag.TipoId = new SelectList(db.Tipos, "TipoId", "Nome", receita.TipoId);
            ViewBag.UtilizadorId = new SelectList(db.Utilizadores, "UtilizadorId", "Nome", receita.UtilizadorId);
			ViewBag.OrigemId = new SelectList(db.Origens, "OrigemId", "Nome", receita.OrigemId);
			ViewBag.DificuldadeId = new SelectList(db.Dificuldades, "DificuldadeId", "Nome");
			ViewBag.CustoId = new SelectList(db.Custos, "CustoId", "Nome");
			return View(receita);
        }

		#endregion Create

		#region Ingredientes

		//
		// GET: /Receitas/Ingredientes/1

		[Authorize]
		public ActionResult Ingredientes ( int id )
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, id) == false)
				return RedirectToAction("Error", "Home", new { Message = "Só pode editar as próprias receitas." }); 

			var valoresIniciais = new List<DTO.IngredienteReceitaDTO>();
			var auxIngredientesReceita =
				from x in db.IngredientesReceita
				where x.ReceitaId == id
				select x;
			
			if( auxIngredientesReceita.Count() == 0 )
			{
				valoresIniciais.Add( new DTO.IngredienteReceitaDTO { Nome = "", Quantidade = "" } );
			} else 
			{
				foreach ( IngredienteReceita ir in auxIngredientesReceita )
				{
					var auxNomes =
						( from i in db.Ingredientes
						  where i.IngredienteId == ir.IngredienteId
						  select i.Nome
						).ToList();
					var auxNome = auxNomes.First();
					valoresIniciais.Add( new DTO.IngredienteReceitaDTO
					{
						Nome = auxNome,
						Quantidade = ir.Quantidade
					} );
				}
			}
			return View(valoresIniciais);
		}

		//
		// POST: /Receitas/Ingredientes/1

		[HttpPost][Authorize]
		public ActionResult Ingredientes ( int id, IEnumerable<FeedMe.DTO.IngredienteReceitaDTO> lir)
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, id) == false )
				return RedirectToAction("Error", "Home", new { Message = "Só pode editar as próprias receitas." }); 
			if (ModelState.IsValid)
			{
				// Eliminar os que já existem
				IQueryable<IngredienteReceita> aux =
					from x in db.IngredientesReceita
					where x.ReceitaId == id
					select x;

				foreach ( IngredienteReceita i in aux )
				{
					db.IngredientesReceita.Remove(i);
				}
				db.SaveChanges();

                foreach ( DTO.IngredienteReceitaDTO ird in lir )
				{
					// Verificar se já existe o ingrediente
					string nomeIng = ird.Nome.Trim();
					int ingId;
					try
					{
						ingId = ( from i in db.Ingredientes
								  where i.Nome == nomeIng
								  select i.IngredienteId ).First();
					} catch
					{
					// Não existe o ingrediente, vou adicionar
						Ingrediente ing =
							new Ingrediente
							{
								Nome = nomeIng
							};
						db.Ingredientes.Add(ing);
						db.SaveChanges();
						ingId = ( from i in db.Ingredientes
								  where i.Nome == nomeIng
								  select i.IngredienteId
								).First();	
					}
					// Adicionar a relação ingrediente-receita
					IngredienteReceita ingReceita =
						new IngredienteReceita
						{
							Quantidade = ird.Quantidade,
							IngredienteId = ingId,
							ReceitaId = id
						};
					db.IngredientesReceita.Add(ingReceita);
					db.SaveChanges();
				}
				return RedirectToAction("Etapas", new { id = id });
			}
			return View(lir);
		}

		public ActionResult IngredienteEditorRow ()
		{
			return PartialView("IngredienteEditorRow");
		}

		#endregion Ingredientes

		#region Etapas

		//
		// GET: /Receitas/Etapas/1

		[Authorize]
		public ActionResult Etapas ( int id )
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, id) == false )
				return RedirectToAction("Error", "Home", new { Message = "Só pode editar as próprias receitas." }); 

			var valoresIniciais = new List<EtapaDTO>();
			var auxEtapas =
				from x in db.Etapas
				where x.ReceitaId == id
				select x;

			if ( auxEtapas.Count() == 0 )
			{
				valoresIniciais.Add(new EtapaDTO { Titulo = "", Descricao = "" });
			} else
			{
				foreach ( Etapa e in auxEtapas )
				{
                    EtapaDTO edto = new EtapaDTO { Titulo = e.Titulo, Descricao = e.Descricao, NomeImagem = e.Imagem };

                    valoresIniciais.Add(edto);
				}
			}
			return View(valoresIniciais);
		}

		//
		// POST: /Receitas/Etapas/1

		[HttpPost]
		[Authorize]
        public ActionResult Etapas(int id, IEnumerable<EtapaDTO> list )
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, id) == false )
				return RedirectToAction("Error", "Home", new { Message = "Só pode editar as próprias receitas." }); 

			if ( ModelState.IsValid )
			{
				// Eliminar os que já existem
				IEnumerable<Etapa> aux =
					from x in db.Etapas
					where x.ReceitaId == id
					select x;

				foreach ( Etapa i in aux )
				{
					db.Etapas.Remove(i);
				}

				int auxNumero = 1;
                foreach (EtapaDTO e in list)
				{
                    Etapa novaEtapa = new Etapa();
                    novaEtapa.Numero = auxNumero;
                    novaEtapa.ReceitaId = id;
                    novaEtapa.Titulo = e.Titulo;
                    novaEtapa.Descricao = e.Descricao;
                    //string fileExt = Path.GetExtension(e.Imagem.FileName);
                    // testar fileExt (.jpg ou semelhante)
					if ( e.Imagem != null )
					{
						string fileName = id.ToString() + "_" + auxNumero.ToString() + e.Imagem.FileName;
						var path = Path.Combine(Server.MapPath("~/Content/uploads"), fileName);
						e.Imagem.SaveAs(path);
						novaEtapa.Imagem = "~/Content/uploads/" + fileName;
                    }
                    else if (e.NomeImagem != null)
                    {
                        novaEtapa.Imagem = e.NomeImagem;
                    }
                    auxNumero++;
                    db.Etapas.Add(novaEtapa);
				}

				db.SaveChanges();
				return RedirectToAction("Details", new { id = id });
			}
			return View(list);
		}
		
		public ActionResult EtapaEditorRow ()
		{
			return PartialView("EtapaEditorRow");
		}
		
		#endregion Etapas
		
		//
		// GET: /Receitas/Resultados/0

		private int PageSize = 4;
		public ActionResult Resultados ( int pageIndex, DTO.Homepage.Pesquisa p ) // é preciso por os parâmetros da pesquisa e fazer o select de acordo com eles
		{
			//var receitas = db.Receitas.Include( x => x.Origem ).Include( x => x.Tipo ).Include( x => x.IngredientesReceita );

			var receitas = from i in db.Receitas
						  select i;

			// Filtrar

			if ( p != null )
			{
				if ( String.IsNullOrWhiteSpace(p.titulo) == false )
					receitas = from r in receitas
							   where ( r.Titulo != null && r.Titulo.Contains(p.titulo) )
							   select r;

				if ( String.IsNullOrWhiteSpace(p.descricao) == false )
					receitas = from r in receitas
							   where ( r.Descricao != null && r.Descricao.Contains(p.descricao) )
							   select r;

				if ( String.IsNullOrWhiteSpace(p.origem) == false )
					receitas = from r in receitas
							   where ( r.Origem != null && r.Origem.Nome.Equals(p.origem, StringComparison.CurrentCultureIgnoreCase) )
							   select r;

				if ( String.IsNullOrWhiteSpace(p.tipo) == false )
					receitas = from r in receitas
							   where ( r.Tipo != null && r.Tipo.Nome.Equals(p.tipo, StringComparison.CurrentCultureIgnoreCase) )
							   select r;

				if ( String.IsNullOrWhiteSpace(p.dificuldade) == false )
					receitas = from r in receitas
							   where ( r.Dificuldade != null && r.Dificuldade.Nome.Equals(p.dificuldade, StringComparison.CurrentCultureIgnoreCase) )
							   select r;

				if ( String.IsNullOrWhiteSpace(p.custo) == false )
					receitas = from r in receitas
							   where ( r.Custo != null && r.Custo.Nome.Equals(p.custo, StringComparison.CurrentCultureIgnoreCase) )
							   select r;

				if ( p.ingrSim != null )
				{
					foreach ( DTO.Homepage.Ingrediente auxIng in p.ingrSim )
					{
						receitas =
							from r in receitas
							join ir in db.IngredientesReceita on r.ReceitaId equals ir.ReceitaId
							where ir.IngredienteId == auxIng.id
							select r;
					}
				}

				if ( p.ingrNao != null )
				{
					var receitasAux = receitas;
					foreach ( Receita r in receitas )
					{
						foreach ( DTO.Homepage.Ingrediente userIng in p.ingrNao )
						{
							var ingsBanidos = from ir in r.IngredientesReceita
											  where ir.IngredienteId == userIng.id
											  select ir;
							if ( ingsBanidos.Count() != 0 )
							{
								receitasAux = receitasAux.Where(x => x.ReceitaId != r.ReceitaId);
							}
						}
					}
					receitas = receitasAux;
				}
				if ( User.Identity.IsAuthenticated && p.ignoreOptions == 0 )
				{
					var user = DatabaseContext.getByName(User.Identity.Name);

					var receitasAux2 = receitas;

					var userIngs = user.Ingredientes;

					foreach ( Receita r in receitas )
					{
						foreach( Ingrediente userIng in userIngs )
						{
							var ingsBanidos = from ir in r.IngredientesReceita
											  where ir.IngredienteId == userIng.IngredienteId
											  select ir;
							if ( ingsBanidos.Count() != 0 )
							{
								receitasAux2 = receitasAux2.Where(x => x.ReceitaId != r.ReceitaId);
							}
						}
					}
					receitas = receitasAux2;
				}
			}
			receitas = receitas.OrderBy(x => x.Titulo);
			int totalCount = receitas.Count();
			int totalPages = (totalCount / PageSize) + ((totalCount % PageSize > 0) ? 1 : 0 );
			int prev, next;

			prev = ( pageIndex > 0 ) ? 1 : 0;
			next = ( pageIndex + 1 < totalPages ) ? 1 : 0;

			IEnumerable<FeedMe.Models.Receita> res = null;
			if ( pageIndex >= totalPages ) {
				res = new List<FeedMe.Models.Receita>();
			} else {
				res = receitas.Skip(pageIndex * PageSize).Take(PageSize).ToList();
			}

			@ViewBag.hasPrev = prev;
			@ViewBag.hasNext = next;
			@ViewBag.pageIndex = pageIndex;

			//return (res.Count() == 0) ?  Content("Empty") :  Content("No Empty"); 
			return PartialView(res);
		}
		/*
		 * 
		//
		// GET: /Receitas/Details/5

		public ActionResult Details ( int? id )
		{
			Receita receita = db.Receitas.Find(id);
			if ( receita == null )
			{
				return HttpNotFound();
			}
			return View(receita);
		}
*/
		[HttpPost]
		[Authorize]
		public ActionResult DeleteComment ( int id )
		{
			if ( db.isAdmin(User.Identity.Name) == false && db.isCommentCreator(User.Identity.Name, id)==false )
				return HttpNotFound(); 

			if ( Request.IsAjaxRequest() )
			{
				Comentario comment = db.Comentarios.Find(id);
				if ( comment == null )
				{
					return HttpNotFound();
				}
				db.Comentarios.Remove(comment);
				db.SaveChanges();
				return Json(id);
			}
			return HttpNotFound();
		}


		//
		// GET: /Receitas/Edit/5

		[Authorize]
        public ActionResult Edit(int id = 0)
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, id) == false )
				return RedirectToAction("Error", "Home", new { Message = "Só pode editar as próprias receitas." }); 

            Receita receita = db.Receitas.Find(id);
            if (receita == null)
            {
                return HttpNotFound();
            }
            ViewBag.TipoId = new SelectList(db.Tipos, "TipoId", "Nome", receita.TipoId);
            ViewBag.UtilizadorId = new SelectList(db.Utilizadores, "UtilizadorId", "Nome", receita.UtilizadorId);
            ViewBag.OrigemId = new SelectList(db.Origens, "OrigemId", "Nome", receita.OrigemId);
			ViewBag.DificuldadeId = new SelectList(db.Dificuldades, "DificuldadeId", "Nome");
			ViewBag.CustoId = new SelectList(db.Custos, "CustoId", "Nome");
			return View(receita);
        }

        //
        // POST: /Receitas/Edit/5

		[HttpPost]
		[Authorize]
        public ActionResult Edit(Receita receita)
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, receita.ReceitaId) == false )
				return RedirectToAction("Error", "Home", new { Message = "Só pode editar as próprias receitas." }); 

            if (ModelState.IsValid)
            {
                db.Entry(receita).State = EntityState.Modified;
				db.SaveChanges();
				return RedirectToAction("Ingredientes", new { id = receita.ReceitaId });
            }
            ViewBag.TipoId = new SelectList(db.Tipos, "TipoId", "Nome", receita.TipoId);
            ViewBag.UtilizadorId = new SelectList(db.Utilizadores, "UtilizadorId", "Nome", receita.UtilizadorId);
			ViewBag.OrigemId = new SelectList(db.Origens, "OrigemId", "Nome", receita.OrigemId);
			ViewBag.DificuldadeId = new SelectList(db.Dificuldades, "DificuldadeId", "Nome");
			ViewBag.CustoId = new SelectList(db.Custos, "CustoId", "Nome");
            return View(receita);
        }

        //
        // GET: /Receitas/Delete/5

		[Authorize]
        public ActionResult Delete(int id = 0)
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, id) == false )
				return RedirectToAction("Error", "Home", new { Message = "Só pode eliminar as próprias receitas." }); 

            Receita receita = db.Receitas.Find(id);
            if (receita == null)
            {
                return HttpNotFound();
            }
            return View(receita);
        }

        //
        // POST: /Receitas/Delete/5

		[Authorize]
        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(int id)
		{
			if ( db.isAdmin(User.Identity.Name) == false && DatabaseContext.isRecipeCreator(User.Identity.Name, id) == false )
				return RedirectToAction("Error", "Home", new { Message = "Só pode eliminar as próprias receitas." }); 

            Receita receita = db.Receitas.Find(id);
            db.Receitas.Remove(receita);
            db.SaveChanges();
            return RedirectToAction("Index","Home");
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }

         // ------------------------------------------ NOVA PARTE ------------------------------------------------

        [HttpPost]
        [Authorize]
        public ActionResult Classificar(DTO.ClassificacaoDTO c)
        {
            var userID = c.utilizadorID;
            var receitaID = c.receitaID;
            var exists = from i in db.Classificacoes
                         where i.ReceitaId == receitaID && i.UtilizadorId == userID
                         select i.ClassificacaoId;

            if (exists.ToList().Count() > 0)
            {
                return Json(false);
            }
            else
            {
                var valor = c.classificacao;
                Classificacao classificacao = new Classificacao { Valor = valor, UtilizadorId = userID, ReceitaId = receitaID };
                db.Classificacoes.Add(classificacao);
                db.SaveChanges();
                return Json(true);
            }
        }

        [HttpPost]
        [Authorize]
        public ActionResult Comentar(DTO.ComentarioDTO c)
        {
            var texto = c.texto;
            var data = System.DateTime.Now;
            var userid = c.userID;
            var receitaid = c.receitaID;

            Comentario comentario = new Comentario { Texto = texto, Data = data, UtilizadorId = userid, ReceitaId = receitaid };
            db.Comentarios.Add(comentario);
            db.SaveChanges();

            return Json(true);
        }

        public ActionResult Comentarios(int id)
        {
            var receita = from x in db.Receitas
                          where x.ReceitaId == id
                          select x;

            return PartialView(receita.ToList().First());
        }

        public ActionResult Estrelas(int id)
        {
            var receita = from x in db.Receitas
                              where x.ReceitaId == id
                              select x;

            return PartialView(receita.ToList().First());
        }

        // ------------------------------------------------------------------------------------------------------

        
    }
}